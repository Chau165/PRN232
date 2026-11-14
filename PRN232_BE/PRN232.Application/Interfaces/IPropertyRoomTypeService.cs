using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyRoomTypeService
{
    Task<List<PropertyRoomTypeDto>> GetAllByPropertyIdAsync(int propertyId, int requestingUserId, string role);
    Task<PropertyRoomTypeDto?> GetByIdAsync(int propertyId, int roomTypeId, int requestingUserId, string role);
    Task<PropertyRoomTypeDto> CreateAsync(int propertyId, CreatePropertyRoomTypeDto dto);
    Task<PropertyRoomTypeDto?> UpdateAsync(int propertyId, int roomTypeId, UpdatePropertyRoomTypeDto dto);
    Task<PropertyRoomTypeDto?> UpdateStatusAsync(int propertyId, int roomTypeId, UpdatePropertyRoomTypeStatusDto dto);
}
