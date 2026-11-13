using System.Globalization;
using System.Text;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class ViewingScheduleService : IViewingScheduleService
{
    private const int MaxManagerNoteLength = 500;
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private readonly IViewingScheduleRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationService? _notificationService;
    private readonly INotificationRealtimePublisher? _realtimePublisher;
    private readonly IUserContactLookup? _contactLookup;

    public ViewingScheduleService(
        IViewingScheduleRepository repository,
        TimeProvider timeProvider,
        INotificationService? notificationService = null,
        INotificationRealtimePublisher? realtimePublisher = null,
        IUserContactLookup? contactLookup = null)
    {
        _contactLookup = contactLookup;
        _repository = repository;
        _timeProvider = timeProvider;
        _notificationService = notificationService;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<ViewingScheduleDto> CreateAsync(CreateViewingScheduleDto dto, int customerId)
    {
        // The Manager has to contact the customer to arrange the viewing.
        await PhoneNumberRequiredException.EnsureAsync(
            _contactLookup, customerId, PhoneNumberRequiredException.SelfMessage);

        var now = _timeProvider.GetUtcNow();
        EnsureFuture(dto.ScheduledAt, now);

        var room = await _repository.GetRoomAsync(dto.RoomId);
        if (room is null)
        {
            throw new ViewingNotFoundException("Room does not exist.");
        }
        if (!await _repository.IsRoomRentableAsync(room.RoomId, now))
        {
            throw new ViewingConflictException("Room is not available for viewing.");
        }
        if (await _repository.HasOpenRequestAsync(customerId, room.RoomId, now))
        {
            throw new ViewingConflictException("You already have an open viewing request for this room.");
        }

        var viewing = ConvertToEntity(dto, room.RoomId, customerId);
        IReadOnlyCollection<int> adminIds = _notificationService is null
            ? Array.Empty<int>()
            : await _notificationService.GetActiveAdminUserIdsAsync();
        IReadOnlyCollection<Notifications> notifications = [];
        await _repository.AddAsync(viewing, viewingId =>
        {
            notifications = adminIds.Distinct().Select(adminId => CreateNotification(
                adminId,
                viewingId,
                $"Có yêu cầu xem phòng {room.RoomCode} mới",
                $"{room.RoomCode} · khách hàng #{customerId} đã gửi yêu cầu xem phòng.")).ToArray();
            return notifications;
        });

        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync(notifications);
        }
        var created = await _repository.GetByIdAsync(viewing.ViewingId, customerId, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created viewing request was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<List<ViewingScheduleDto>> GetAllAsync(int userId, string role, string? status)
    {
        if (status is not null && !ViewingStatuses.IsValid(status))
        {
            throw new ViewingBusinessRuleException("Status must be Pending, Approved, or Rejected.");
        }

        var (customerId, assignedTo) = GetScope(userId, role);
        var viewings = await _repository.GetAllAsync(customerId, assignedTo, status);
        return viewings.Select(ConvertToDto).ToList();
    }

    public async Task<ViewingScheduleDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, assignedTo) = GetScope(userId, role);
        var viewing = await _repository.GetByIdAsync(id, customerId, assignedTo);
        if (viewing is null && (role is RoleNames.Customer or RoleNames.Manager) && await _repository.ExistsAsync(id))
        {
            throw new ViewingAuthorizationException("You cannot access this viewing request.");
        }
        return viewing is null ? null : ConvertToDto(viewing);
    }

    public async Task<ViewingScheduleDto> ApproveAsync(int id, ApproveViewingScheduleDto dto, int userId, string role)
    {
        EnsureAdmin(role);
        var viewing = await GetPendingForAdminAsync(id, userId);
        if (dto.AssignedTo is not > 0)
        {
            throw new ViewingBusinessRuleException("An active Manager is required for assignment.");
        }

        var manager = await _repository.GetManagerAsync(dto.AssignedTo.Value);
        if (manager is null || !manager.IsActive || manager.RoleName != RoleNames.Manager)
        {
            throw new ViewingBusinessRuleException("Assignee must be an active Manager.");
        }
        if (!await _repository.HasActiveManagerAssignmentAsync(viewing.PropertyId, manager.UserId))
        {
            throw new ViewingBusinessRuleException("Manager must have an active assignment to this property.");
        }

        var managerNote = NormalizeNote(dto.ManagerNote);
        var now = _timeProvider.GetUtcNow();
        EnsureFuture(dto.ScheduledAt, now);
        var scheduledAtUtc = dto.ScheduledAt?.UtcDateTime ?? viewing.ScheduledAt;
        if (!scheduledAtUtc.HasValue || scheduledAtUtc.Value <= now.UtcDateTime)
        {
            throw new ViewingBusinessRuleException("Choose a future viewing time before approving.");
        }

        var scheduledAtText = FormatVietnamTime(scheduledAtUtc.Value);
        var customerBody = $"Lịch xem phòng {viewing.RoomCode} đã được xác nhận lúc {scheduledAtText}.";
        if (managerNote is not null)
        {
            customerBody += $" Ghi chú: {managerNote}";
        }
        var notifications = new[]
        {
            CreateNotification(
                viewing.CustomerId,
                viewing.ViewingId,
                $"Lịch xem phòng {viewing.RoomCode} đã được chấp thuận",
                customerBody),
            CreateNotification(
                manager.UserId,
                viewing.ViewingId,
                $"Bạn được giao lịch xem phòng {viewing.RoomCode}",
                managerNote ?? $"Thời gian đã xác nhận: {scheduledAtText}.")
        };

        return await DecideAsync(
            viewing,
            ViewingStatuses.Approved,
            manager.UserId,
            managerNote,
            userId,
            now.UtcDateTime,
            scheduledAtUtc,
            notifications);
    }

    public async Task<ViewingScheduleDto> RejectAsync(int id, RejectViewingScheduleDto dto, int userId, string role)
    {
        EnsureAdmin(role);
        var viewing = await GetPendingForAdminAsync(id, userId);
        var reason = NormalizeNote(dto.Reason, required: true)!;

        var now = _timeProvider.GetUtcNow();
        var notification = CreateNotification(
            viewing.CustomerId,
            viewing.ViewingId,
            $"Lịch xem phòng {viewing.RoomCode} đã bị từ chối",
            reason);
        return await DecideAsync(
            viewing,
            ViewingStatuses.Rejected,
            null,
            reason,
            userId,
            now.UtcDateTime,
            null,
            [notification]);
    }

    private async Task<ViewingScheduleDto> GetPendingForAdminAsync(int id, int userId)
    {
        var viewing = await GetByIdAsync(id, userId, RoleNames.Admin);
        if (viewing is null)
        {
            throw new ViewingNotFoundException("Viewing request does not exist.");
        }
        if (viewing.Status != ViewingStatuses.Pending)
        {
            throw new ViewingConflictException("Only pending requests can be handled.");
        }
        return viewing;
    }

    private async Task<ViewingScheduleDto> DecideAsync(
        ViewingScheduleDto viewing,
        string status,
        int? assignedTo,
        string? managerNote,
        int respondedBy,
        DateTime respondedAtUtc,
        DateTime? scheduledAtUtc,
        IReadOnlyCollection<Notifications> notifications)
    {
        if (!await _repository.DecideAsync(
                viewing.ViewingId,
                status,
                assignedTo,
                managerNote,
                respondedBy,
                respondedAtUtc,
                scheduledAtUtc,
                notifications))
        {
            throw new ViewingConflictException("Viewing request has already been handled.");
        }
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync(notifications);
        }
        var updated = await _repository.GetByIdAsync(viewing.ViewingId, null, null);
        if (updated is null)
        {
            throw new InvalidOperationException("Handled viewing request was not found.");
        }
        return ConvertToDto(updated);
    }

    private static Notifications CreateNotification(int userId, int viewingId, string title, string body) => new()
    {
        UserId = userId,
        Title = title,
        Body = body,
        Type = NotificationTypes.Viewing,
        RelatedEntityId = viewingId,
        IsRead = false
    };

    // Notifications are read by people: show Vietnam local time, e.g. "14:30 15/11/2026".
    private static string FormatVietnamTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BusinessTimeZone)
            .ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? NormalizeNote(string? note, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            if (required)
            {
                throw new ViewingBusinessRuleException("A rejection reason is required.");
            }
            return null;
        }
        var normalized = note.Trim();
        if (required && normalized.Length == 0)
        {
            throw new ViewingBusinessRuleException("A rejection reason is required.");
        }
        if (CountCharacters(normalized) > MaxManagerNoteLength)
        {
            throw new ViewingBusinessRuleException($"Manager note must not exceed {MaxManagerNoteLength} characters.");
        }
        return normalized;
    }

    private static int CountCharacters(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }
        return count;
    }

    private static void EnsureAdmin(string role)
    {
        if (role != RoleNames.Admin)
        {
            throw new ViewingAuthorizationException("Only Admins can approve or reject viewing requests.");
        }
    }

    private static void EnsureFuture(DateTimeOffset? scheduledAt, DateTimeOffset now)
    {
        if (scheduledAt is { } value && value <= now)
        {
            throw new ViewingBusinessRuleException("Viewing time must be in the future.");
        }
    }

    private static ViewingSchedules ConvertToEntity(CreateViewingScheduleDto dto, int roomId, int customerId) => new()
    {
        RoomId = roomId,
        CustomerId = customerId,
        ScheduledAt = dto.ScheduledAt?.UtcDateTime,
        Status = ViewingStatuses.Pending
    };

    private static (int? CustomerId, int? AssignedTo) GetScope(int userId, string role) => role switch
    {
        RoleNames.Customer => (userId, null),
        RoleNames.Manager => (null, userId),
        RoleNames.Admin => (null, null),
        _ => throw new ViewingAuthorizationException("Your role cannot access viewing requests.")
    };

    private static ViewingScheduleDto ConvertToDto(ViewingScheduleReadModel viewing) => new(
        viewing.ViewingId,
        viewing.RoomId,
        viewing.RoomCode,
        viewing.PropertyId,
        viewing.PropertyName,
        viewing.CustomerId,
        viewing.CustomerName,
        viewing.CustomerPhone,
        viewing.ScheduledAt,
        viewing.Status,
        viewing.AssignedTo,
        viewing.AssignedManagerName,
        viewing.AssignedManagerPhone,
        viewing.ManagerNote,
        viewing.RespondedBy,
        viewing.RespondedAt);
}

public sealed class ViewingNotFoundException(string message) : Exception(message);

public sealed class ViewingAuthorizationException(string message) : Exception(message);

public sealed class ViewingBusinessRuleException(string message) : Exception(message);

public sealed class ViewingConflictException(string message) : Exception(message);
