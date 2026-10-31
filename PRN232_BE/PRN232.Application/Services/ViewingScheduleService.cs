using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class ViewingScheduleService : IViewingScheduleService
{
    private readonly IViewingScheduleRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ViewingScheduleService(IViewingScheduleRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ViewingScheduleDto> CreateAsync(CreateViewingScheduleDto dto, int customerId)
    {
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
        await _repository.AddAsync(viewing, new Notifications
        {
            UserId = room.OwnerId,
            Title = $"Có yêu cầu xem phòng {room.RoomCode} mới",
            Type = NotificationTypes.Viewing,
            IsRead = false
        });

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

        var (customerId, ownerId) = GetScope(userId, role);
        var viewings = await _repository.GetAllAsync(customerId, ownerId, status);
        return viewings.Select(ConvertToDto).ToList();
    }

    public async Task<ViewingScheduleDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var viewing = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (viewing is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new ViewingAuthorizationException("You cannot access this viewing request.");
        }
        if (viewing is null)
        {
            return null;
        }
        return ConvertToDto(viewing);
    }

    // Gộp "Chọn lịch" và "Chấp thuận": nếu khách để chủ trọ sắp xếp thì phải gửi kèm giờ xem;
    // nếu khách đã chọn giờ, quản lý vẫn có thể đổi sang giờ khác khi chấp thuận.
    public async Task<ViewingScheduleDto> ApproveAsync(int id, ApproveViewingScheduleDto dto, int userId, string role)
    {
        var viewing = await GetPendingForStaffAsync(id, userId, role);
        var now = _timeProvider.GetUtcNow();
        EnsureFuture(dto.ScheduledAt, now);
        var newTimeUtc = dto.ScheduledAt?.UtcDateTime;
        DateTime effectiveTime;
        if (newTimeUtc.HasValue)
        {
            effectiveTime = newTimeUtc.Value;
        }
        else if (viewing.ScheduledAt.HasValue)
        {
            effectiveTime = viewing.ScheduledAt.Value;
        }
        else
        {
            throw new ViewingBusinessRuleException("Choose a viewing time before approving.");
        }
        if (effectiveTime <= now.UtcDateTime)
        {
            throw new ViewingBusinessRuleException("The requested viewing time has passed. Choose a new time.");
        }

        // newTimeUtc = null thì repository giữ nguyên giờ khách đã chọn.
        return await DecideAsync(viewing, ViewingStatuses.Approved, newTimeUtc,
            $"Lịch xem phòng {viewing.RoomCode} đã được chấp thuận");
    }

    public async Task<ViewingScheduleDto> RejectAsync(int id, int userId, string role)
    {
        var viewing = await GetPendingForStaffAsync(id, userId, role);
        return await DecideAsync(viewing, ViewingStatuses.Rejected, null,
            $"Lịch xem phòng {viewing.RoomCode} đã bị từ chối");
    }

    private async Task<ViewingScheduleDto> GetPendingForStaffAsync(int id, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new ViewingAuthorizationException("Only property managers can handle viewing requests.");
        }

        var viewing = await GetByIdAsync(id, userId, role);
        if (viewing is null)
        {
            throw new ViewingNotFoundException("Viewing request does not exist.");
        }
        if (viewing.Status != ViewingStatuses.Pending)
        {
            throw new ViewingConflictException("Only pending viewing requests can be handled.");
        }
        return viewing;
    }

    private async Task<ViewingScheduleDto> DecideAsync(
        ViewingScheduleDto viewing, string status, DateTime? scheduledAtUtc, string notificationTitle)
    {
        var notification = new Notifications
        {
            UserId = viewing.CustomerId,
            Title = notificationTitle,
            Type = NotificationTypes.Viewing,
            IsRead = false
        };
        // Repository chỉ cập nhật khi yêu cầu vẫn Pending, phòng khi hai quản lý xử lý cùng lúc.
        if (!await _repository.DecideAsync(viewing.ViewingId, status, scheduledAtUtc, notification))
        {
            throw new ViewingConflictException("Viewing request has already been handled.");
        }

        var updated = await _repository.GetByIdAsync(viewing.ViewingId, null, null);
        if (updated is null)
        {
            throw new InvalidOperationException("Handled viewing request was not found.");
        }
        return ConvertToDto(updated);
    }

    private static void EnsureFuture(DateTimeOffset? scheduledAt, DateTimeOffset now)
    {
        if (scheduledAt is { } value && value <= now)
        {
            throw new ViewingBusinessRuleException("Viewing time must be in the future.");
        }
    }

    private static ViewingSchedules ConvertToEntity(CreateViewingScheduleDto dto, int roomId, int customerId)
    {
        return new ViewingSchedules
        {
            RoomId = roomId,
            CustomerId = customerId,
            ScheduledAt = dto.ScheduledAt?.UtcDateTime,
            Status = ViewingStatuses.Pending
        };
    }

    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new ViewingAuthorizationException("Your role cannot access viewing requests.");
        }
        return scope.Value;
    }

    private static ViewingScheduleDto ConvertToDto(ViewingScheduleReadModel viewing)
    {
        return new ViewingScheduleDto(
            viewing.ViewingId,
            viewing.RoomId,
            viewing.RoomCode,
            viewing.PropertyId,
            viewing.PropertyName,
            viewing.CustomerId,
            viewing.CustomerName,
            viewing.CustomerPhone,
            viewing.ScheduledAt,
            viewing.Status);
    }
}

public sealed class ViewingNotFoundException(string message) : Exception(message);

public sealed class ViewingAuthorizationException(string message) : Exception(message);

public sealed class ViewingBusinessRuleException(string message) : Exception(message);

public sealed class ViewingConflictException(string message) : Exception(message);
