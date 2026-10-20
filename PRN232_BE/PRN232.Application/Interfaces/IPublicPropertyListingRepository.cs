using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPublicPropertyListingRepository
{
    Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken);

    Task<PublicPropertyPageDto> GetPageAsync(
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicPropertyAreaCardDto>> GetByAreaAsync(
        PublicPropertyAreaSearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicPropertyDetailDto?> GetDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicPropertyDetailDto?> GetEnrichedDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        DateOnly currentMonthStart,
        CancellationToken cancellationToken);

    Task<PublicPropertyRoomPageDto?> GetRoomsAsync(
        int propertyId,
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicRoomDetailDto?> GetRoomDetailAsync(
        int roomId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}
