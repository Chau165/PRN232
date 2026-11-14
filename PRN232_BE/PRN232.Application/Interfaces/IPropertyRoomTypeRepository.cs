using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IPropertyRoomTypeRepository
{
    Task<List<PropertyRoomTypeReadModel>> GetAllByPropertyIdAsync(int propertyId, DateTimeOffset asOfUtc);
    Task<PropertyRoomTypeReadModel?> GetByIdAsync(int propertyId, int roomTypeId, DateTimeOffset asOfUtc);
    Task<PropertyRoomTypes?> GetTrackedByIdAsync(int propertyId, int roomTypeId);
    Task<bool> PropertyExistsAsync(int propertyId);
    Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId);
    Task<bool> RoomTypeExistsAsync(int roomTypeId);
    Task<bool> ExistsAsync(int propertyId, int roomTypeId);
    Task<bool> IsActiveAsync(int propertyId, int roomTypeId);
    Task AddAsync(PropertyRoomTypes propertyRoomType);
    Task SaveChangesAsync();
}
