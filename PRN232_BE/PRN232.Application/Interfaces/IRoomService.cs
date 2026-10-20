using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRoomService
{
    Task<List<RoomDto>> GetAllAsync(int? propertyId, string? status, string? city, decimal? maxPrice);
    Task<RoomDto?> GetByIdAsync(int id);
    Task<List<RentableRoomDto>> GetRentableRoomsAsync(DateTimeOffset asOfUtc);
    Task<List<PropertyAvailabilityDto>> GetPropertyAvailabilityAsync(DateTimeOffset asOfUtc);
    Task<RoomDto> CreateAsync(CreateRoomDto dto, int requestingUserId, bool isAdmin);
    Task<RoomDto?> UpdateAsync(int id, UpdateRoomDto dto, int requestingUserId, bool isAdmin);
    Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin);

    Task<List<RoomImageDto>?> GetImagesAsync(int roomId);
    Task<RoomImageDto?> AddImageAsync(int roomId, AddRoomImageDto dto, int requestingUserId, bool isAdmin);
    Task<bool> RemoveImageAsync(int roomId, int imageId, int requestingUserId, bool isAdmin);

    Task<List<AmenityDto>?> GetAmenitiesAsync(int roomId);
    Task<bool> AddAmenityAsync(int roomId, int amenityId, int requestingUserId, bool isAdmin);
    Task<bool> RemoveAmenityAsync(int roomId, int amenityId, int requestingUserId, bool isAdmin);
}
