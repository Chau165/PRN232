using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRoomTypeService
{
    Task<List<RoomTypeDto>> GetAllAsync();
    Task<RoomTypeDto?> GetByIdAsync(int id);
    Task<RoomTypeDto> CreateAsync(CreateRoomTypeDto dto);
    Task<RoomTypeDto?> UpdateAsync(int id, UpdateRoomTypeDto dto);
    Task<bool> DeleteAsync(int id);

    Task<List<ServiceDto>?> GetServicesAsync(int roomTypeId);
    Task<bool> AddServiceAsync(int roomTypeId, int serviceId);
    Task<bool> RemoveServiceAsync(int roomTypeId, int serviceId);
}
