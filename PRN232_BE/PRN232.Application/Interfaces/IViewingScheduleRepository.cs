using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IViewingScheduleRepository
{
    Task<ViewingRoomReadModel?> GetRoomAsync(int roomId);
    Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc);
    Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc);
    Task AddAsync(ViewingSchedules viewing, Notifications ownerNotification);
    Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status);
    Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? ownerId);
    Task<bool> ExistsAsync(int viewingId);
    Task<bool> DecideAsync(int viewingId, string status, DateTime? scheduledAtUtc, Notifications customerNotification);
}
