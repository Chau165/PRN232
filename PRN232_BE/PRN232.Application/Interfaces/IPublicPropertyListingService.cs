using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPublicPropertyListingService
{
    Task<PublicPropertyPageDto> GetAllAsync(PublicPropertyQueryDto query, CancellationToken cancellationToken);

    Task<PublicPropertyDetailDto?> GetDetailAsync(int propertyId, CancellationToken cancellationToken);

    Task<PublicPropertyRoomPageDto?> GetRoomsAsync(
        int propertyId,
        PublicPropertyRoomsQueryDto query,
        CancellationToken cancellationToken);

    Task<PublicRoomDetailDto?> GetRoomDetailAsync(int roomId, CancellationToken cancellationToken);
}
