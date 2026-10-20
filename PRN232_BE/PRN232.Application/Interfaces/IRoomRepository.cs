using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IRoomRepository
{
    Task<List<RoomDto>> GetAllAsync(int? propertyId, string? status, string? city, decimal? maxPrice);
    Task<RoomDto?> GetByIdAsync(int id);
    Task<List<RentableRoomDto>> GetRentableRoomsAsync(DateTimeOffset asOfUtc);
    Task<List<PropertyAvailabilityDto>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc);
    Task<Rooms?> GetEntityByIdAsync(int id);
    Task<Rooms> AddAsync(Rooms room);
    void Remove(Rooms room);
    Task<int?> GetOwnerIdAsync(int roomId);
    Task<bool> PropertyExistsAsync(int propertyId);
    Task<int?> GetPropertyOwnerIdAsync(int propertyId);
    Task<bool> RoomTypeExistsAsync(int roomTypeId);
    Task<bool> HasDependentsAsync(int roomId);
    Task SaveChangesAsync();

    // ảnh phòng
    Task<List<RoomImageDto>> GetImagesAsync(int roomId);
    Task<RoomImages> AddImageAsync(int roomId, RoomImages image);
    Task<bool> RemoveImageAsync(int roomId, int imageId);

    // tiện ích gắn cho phòng (RoomAmenities)
    Task<List<AmenityDto>> GetAmenitiesAsync(int roomId);
    Task<bool> AmenityLinkExistsAsync(int roomId, int amenityId);
    Task AddAmenityLinkAsync(int roomId, int amenityId);
    Task<bool> RemoveAmenityLinkAsync(int roomId, int amenityId);
    Task<bool> AmenityExistsAsync(int amenityId);
}
