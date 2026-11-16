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

        int propertyId;
        int roomTypeId;
        int? selectedRoomId = null;
        string? selectedRoomCode = null;
        string propertyName;
        string roomTypeName;

        if (dto.RoomId is int roomId)
        {
            var room = await _repository.GetRoomAsync(roomId);
            if (room is null)
            {
                throw new ViewingNotFoundException("Room does not exist.");
            }
            if ((dto.PropertyId.HasValue && dto.PropertyId != room.PropertyId) ||
                (dto.RoomTypeId.HasValue && dto.RoomTypeId != room.RoomTypeId))
            {
                throw new ViewingBusinessRuleException("The supplied room does not match the requested property and room type.");
            }
            if (!await _repository.IsRoomRentableAsync(room.RoomId, now))
            {
                throw new ViewingConflictException("Room is not available for viewing.");
            }

            propertyId = room.PropertyId;
            roomTypeId = room.RoomTypeId;
            selectedRoomId = room.RoomId;
            selectedRoomCode = room.RoomCode;
            var propertyRoomType = await _repository.GetActivePropertyRoomTypeAsync(propertyId, roomTypeId);
            if (propertyRoomType is null)
            {
                throw new ViewingConflictException("This room type is not active for the selected property.");
            }
            propertyName = propertyRoomType.PropertyName;
            roomTypeName = propertyRoomType.RoomTypeName;
        }
        else
        {
            if (dto.PropertyId is not > 0 || dto.RoomTypeId is not > 0)
            {
                throw new ViewingBusinessRuleException("PropertyId and RoomTypeId are required when RoomId is not supplied.");
            }

            var propertyRoomType = await _repository.GetActivePropertyRoomTypeAsync(dto.PropertyId.Value, dto.RoomTypeId.Value);
            if (propertyRoomType is null)
            {
                throw new ViewingConflictException("This room type is not active for the selected property.");
            }
            propertyId = propertyRoomType.PropertyId;
            roomTypeId = propertyRoomType.RoomTypeId;
            propertyName = propertyRoomType.PropertyName;
            roomTypeName = propertyRoomType.RoomTypeName;
        }

        if (await _repository.HasOpenRequestForRoomTypeAsync(customerId, propertyId, roomTypeId, now))
        {
            throw new ViewingConflictException("You already have an open viewing request for this room type at this property.");
        }

        var viewing = ConvertToEntity(propertyId, roomTypeId, selectedRoomId, dto, customerId);
        // Direct Manager approval: notify only active Managers of this viewing's property.
        var managerIds = await _repository.GetActiveManagerIdsByPropertyAsync(propertyId);
        if (managerIds.Count == 0)
        {
            throw new ViewingConflictException("This property has no active assigned Manager to handle viewing requests.");
        }
        IReadOnlyCollection<Notifications> notifications = [];
        await _repository.AddAsync(viewing, viewingId =>
        {
            notifications = managerIds.Distinct().Select(managerId => CreateNotification(
                managerId,
                viewingId,
                selectedRoomCode is null ? "Có yêu cầu xem loại phòng mới" : $"Có yêu cầu xem phòng {selectedRoomCode}",
                selectedRoomCode is null
                    ? $"{propertyName} · {roomTypeName} · khách hàng #{customerId} đã gửi yêu cầu xem phòng."
                    : $"Khách hàng #{customerId} đã gửi yêu cầu xem {selectedRoomCode}.")).ToArray();
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

    public async Task<List<ViewingAvailableRoomDto>> GetAvailableRoomsAsync(int viewingId, int userId, string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new ViewingAuthorizationException("Only the assigned Manager can select a room for this viewing.");
        }

        var viewing = await _repository.GetByIdAsync(viewingId, null, userId);
        if (viewing is null)
        {
            if (await _repository.ExistsAsync(viewingId))
            {
                throw new ViewingAuthorizationException("You are no longer assigned to this viewing or property.");
            }
            throw new ViewingNotFoundException("Viewing request does not exist.");
        }
        if (viewing.AssignedTo != userId)
        {
            throw new ViewingAuthorizationException("Only the Manager who approved this viewing can select its room.");
        }
        if (viewing.Status != ViewingStatuses.Approved || viewing.RoomId.HasValue)
        {
            return [];
        }

        var rooms = await _repository.GetAvailableRoomsAsync(
            viewing.PropertyId,
            viewing.RoomTypeId,
            _timeProvider.GetUtcNow());
        return rooms.Select(room => new ViewingAvailableRoomDto(
            room.RoomId,
            room.RoomCode,
            room.AreaSqm,
            room.Status)).ToList();
    }

    public async Task<ViewingScheduleDto> SelectRoomAsync(
        int viewingId,
        SelectViewingRoomDto dto,
        int userId,
        string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new ViewingAuthorizationException("Only the assigned Manager can select a room for this viewing.");
        }

        var viewing = await _repository.GetByIdAsync(viewingId, null, userId);
        if (viewing is null)
        {
            if (await _repository.ExistsAsync(viewingId))
            {
                throw new ViewingAuthorizationException("You are no longer assigned to this viewing or property.");
            }
            throw new ViewingNotFoundException("Viewing request does not exist.");
        }
        if (viewing.AssignedTo != userId)
        {
            throw new ViewingAuthorizationException("Only the Manager who approved this viewing can select its room.");
        }
        if (viewing.Status != ViewingStatuses.Approved)
        {
            throw new ViewingConflictException("Only approved viewing requests can have a selected room.");
        }
        if (viewing.RoomId.HasValue)
        {
            if (viewing.RoomId.Value == dto.RoomId)
            {
                return ConvertToDto(viewing);
            }
            throw new ViewingConflictException("A physical room has already been selected for this viewing.");
        }

        var notification = CreateNotification(
            viewing.CustomerId,
            viewing.ViewingId,
            "Phòng dẫn khách xem đã được xác nhận",
            $"Manager đã chọn phòng cho {viewing.PropertyName} · {viewing.RoomTypeName}. Vui lòng xem chi tiết lịch hẹn.");
        if (!await _repository.SelectRoomAsync(viewingId, userId, dto.RoomId, _timeProvider.GetUtcNow(), notification))
        {
            throw new ViewingConflictException("The room is unavailable or this viewing changed. Reload and try again.");
        }

        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        var updated = await _repository.GetByIdAsync(viewingId, null, userId);
        if (updated is null)
        {
            throw new ViewingAuthorizationException("You are no longer assigned to this viewing or property.");
        }
        return ConvertToDto(updated);
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
        EnsureManager(role);
        var viewing = await GetPendingForManagerAsync(id, userId);
        if (!await _repository.HasActiveManagerAssignmentAsync(viewing.PropertyId, userId))
        {
            throw new ViewingAuthorizationException("You are not an active Manager of this property.");
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
        var viewingDescription = $"{viewing.PropertyName} · {viewing.RoomTypeName}";
        var customerBody = $"Lịch xem {viewingDescription} đã được xác nhận lúc {scheduledAtText}.";
        if (managerNote is not null)
        {
            customerBody += $" Ghi chú: {managerNote}";
        }

        return await DecideAsync(
            viewing,
            ViewingStatuses.Approved,
            userId,
            managerNote,
            userId,
            now.UtcDateTime,
            scheduledAtUtc,
            [CreateNotification(viewing.CustomerId, viewing.ViewingId,
                $"Lịch xem {viewingDescription} đã được chấp thuận", customerBody)]);
    }

    public async Task<ViewingScheduleDto> RejectAsync(int id, RejectViewingScheduleDto dto, int userId, string role)
    {
        EnsureManager(role);
        var viewing = await GetPendingForManagerAsync(id, userId);
        if (!await _repository.HasActiveManagerAssignmentAsync(viewing.PropertyId, userId))
        {
            throw new ViewingAuthorizationException("You are not an active Manager of this property.");
        }

        var reason = NormalizeNote(dto.Reason, required: true)!;
        var now = _timeProvider.GetUtcNow();
        var notification = CreateNotification(
            viewing.CustomerId,
            viewing.ViewingId,
            $"Lịch xem {viewing.PropertyName} · {viewing.RoomTypeName} đã bị từ chối",
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

    private async Task<ViewingScheduleDto> GetPendingForManagerAsync(int id, int userId)
    {
        var viewing = await GetByIdAsync(id, userId, RoleNames.Manager);
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

    private static void EnsureManager(string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new ViewingAuthorizationException("Only Managers assigned to the property can approve or reject viewing requests.");
        }
    }

    private static void EnsureFuture(DateTimeOffset? scheduledAt, DateTimeOffset now)
    {
        if (scheduledAt is { } value && value <= now)
        {
            throw new ViewingBusinessRuleException("Viewing time must be in the future.");
        }
    }

    private static ViewingSchedules ConvertToEntity(
        int propertyId,
        int roomTypeId,
        int? roomId,
        CreateViewingScheduleDto dto,
        int customerId) => new()
    {
        PropertyId = propertyId,
        RoomTypeId = roomTypeId,
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
        viewing.RoomTypeId,
        viewing.RoomTypeName,
        viewing.CurrentMonthlyRent,
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
