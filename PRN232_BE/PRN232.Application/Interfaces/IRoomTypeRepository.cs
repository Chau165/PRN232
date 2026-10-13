using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IRoomTypeRepository
{
    Task<List<RoomTypeDto>> GetAllAsync();
    Task<RoomTypeDto?> GetByIdAsync(int id);
    Task<RoomTypes?> GetEntityByIdAsync(int id);
    Task<RoomTypes> AddAsync(RoomTypes roomType);
    void Remove(RoomTypes roomType);
    Task<bool> HasRoomsAsync(int roomTypeId);
    Task SaveChangesAsync();

    // gắn/gỡ dịch vụ cho loại phòng (RoomTypeServices)
    Task<List<ServiceDto>> GetServicesAsync(int roomTypeId);
    Task<bool> ServiceLinkExistsAsync(int roomTypeId, int serviceId);
    Task AddServiceLinkAsync(int roomTypeId, int serviceId);
    Task<bool> RemoveServiceLinkAsync(int roomTypeId, int serviceId);
}
