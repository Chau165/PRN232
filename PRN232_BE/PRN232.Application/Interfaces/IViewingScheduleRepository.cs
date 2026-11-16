using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IViewingScheduleRepository
{
    Task<ViewingRoomReadModel?> GetRoomAsync(int roomId);
    Task<string?> GetCustomerNameAsync(int customerId);
    Task<ViewingPropertyRoomTypeReadModel?> GetActivePropertyRoomTypeAsync(int propertyId, int roomTypeId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc);
    Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc);
    Task<bool> HasOpenRequestForRoomTypeAsync(int customerId, int propertyId, int roomTypeId, DateTimeOffset asOfUtc);
    Task<List<ViewingAvailableRoomReadModel>> GetAvailableRoomsAsync(int propertyId, int roomTypeId, DateTimeOffset asOfUtc);
    Task<bool> SelectRoomAsync(int viewingId, int managerId, int roomId, DateTimeOffset asOfUtc, Notifications notification);
    Task<bool> AddAsync(
        ViewingSchedules viewing,
        Func<int, IReadOnlyCollection<int>, IReadOnlyCollection<Notifications>> notificationFactory);
    Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? managerId, string? status);
    Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? managerId);
    Task<bool> ExistsAsync(int viewingId);
    Task<bool> DecideAsync(
        int viewingId,
        string status,
        int managerId,
        string? managerNote,
        DateTime respondedAtUtc,
        DateTime? scheduledAtUtc,
        IReadOnlyCollection<Notifications> notifications);
}
