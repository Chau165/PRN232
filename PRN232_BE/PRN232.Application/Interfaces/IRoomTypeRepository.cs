using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IRoomTypeRepository
{
    Task<List<RoomTypes>> GetAllAsync();
    Task<RoomTypes?> GetByIdAsync(int id);
    Task<RoomTypes?> GetTrackedByIdAsync(int id);
    Task<RoomTypes> AddAsync(RoomTypes roomType);
    void Remove(RoomTypes roomType);
    Task<bool> HasRoomsAsync(int roomTypeId);
    Task SaveChangesAsync();

    // gắn/gỡ dịch vụ cho loại phòng (RoomTypeServices)
    Task<List<Service>> GetServicesAsync(int roomTypeId);
    Task<bool> ServiceLinkExistsAsync(int roomTypeId, int serviceId);
    Task AddServiceLinkAsync(int roomTypeId, int serviceId);
    Task<bool> RemoveServiceLinkAsync(int roomTypeId, int serviceId);
}
