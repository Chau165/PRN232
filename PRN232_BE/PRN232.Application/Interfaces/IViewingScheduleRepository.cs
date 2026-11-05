using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IViewingScheduleRepository
{
    Task<ViewingRoomReadModel?> GetRoomAsync(int roomId);
    Task<ViewingManagerReadModel?> GetManagerAsync(int userId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc);
    Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc);
    Task AddAsync(ViewingSchedules viewing, Func<int, IReadOnlyCollection<Notifications>> notificationFactory);
    Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? assignedTo, string? status);
    Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? assignedTo);
    Task<bool> ExistsAsync(int viewingId);
    Task<bool> DecideAsync(
        int viewingId,
        string status,
        int? assignedTo,
        string? managerNote,
        int respondedBy,
        DateTime respondedAtUtc,
        DateTime? scheduledAtUtc,
        IReadOnlyCollection<Notifications> notifications);
}
