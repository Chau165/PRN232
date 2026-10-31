using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IPublicPropertyListingRepository
{
    Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken);

    Task<PublicPropertyPageReadModel> GetPageAsync(
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicPropertyAreaCardReadModel>> GetByAreaAsync(
        PublicPropertyAreaSearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicPropertyDetailReadModel?> GetDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicPropertyDetailReadModel?> GetEnrichedDetailAsync(
        int propertyId,
        DateTimeOffset asOfUtc,
        DateOnly currentMonthStart,
        CancellationToken cancellationToken);

    Task<PublicPropertyRoomPageReadModel?> GetRoomsAsync(
        int propertyId,
        PublicPropertySearchCriteria criteria,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);

    Task<PublicRoomDetailReadModel?> GetRoomDetailAsync(
        int roomId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}
