using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PublicPropertyListingServiceTests
{
    [Theory]
    [InlineData(2026, 10, 31, 16, 59, 2026, 10)]
    [InlineData(2026, 10, 31, 17, 0, 2026, 11)]
    public async Task Enriched_detail_uses_vietnamese_current_month_start(
        int utcYear,
        int utcMonth,
        int utcDay,
        int utcHour,
        int utcMinute,
        int expectedYear,
        int expectedMonth)
    {
        var repository = new CapturingPublicPropertyListingRepository();
        var asOfUtc = new DateTimeOffset(
            utcYear,
            utcMonth,
            utcDay,
            utcHour,
            utcMinute,
            0,
            TimeSpan.Zero);
        var service = new PublicPropertyListingService(
            repository,
            new FixedTimeProvider(asOfUtc),
            propertyDetailEnrichmentEnabled: true);

        await service.GetDetailAsync(1, CancellationToken.None);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, 1), repository.CurrentMonthStart);
    }

    [Fact]
    public async Task Room_type_listing_preserves_property_scoped_rent_and_counts()
    {
        var repository = new CapturingPublicPropertyListingRepository
        {
            RoomTypesToReturn =
            [
                new PublicPropertyRoomTypeReadModel(
                    10,
                    "Property A",
                    3,
                    "Premium",
                    3_000_000m,
                    2,
                    4,
                    18m,
                    22m,
                    null)
            ]
        };
        var service = new PublicPropertyListingService(repository, TimeProvider.System);

        var result = await service.GetRoomTypesAsync(
            10,
            new PublicPropertyRoomsQueryDto { SortBy = "price_asc" },
            CancellationToken.None);

        var roomType = Assert.Single(result!);
        Assert.Equal("Property A", roomType.PropertyName);
        Assert.Equal(3_000_000m, roomType.MonthlyRent);
        Assert.Equal(2, roomType.AvailableRoomCount);
        Assert.Equal(4, roomType.TotalRoomCount);
    }

    private sealed class CapturingPublicPropertyListingRepository : IPublicPropertyListingRepository
    {
        public List<PublicPropertyRoomTypeReadModel>? RoomTypesToReturn { get; init; }
        public DateOnly? CurrentMonthStart
        {
            get; private set;
        }

        public Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyPageReadModel> GetPageAsync(
            PublicPropertySearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PublicAreaOptionReadModel>> GetAreaOptionsAsync(
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PublicPropertyAreaCardReadModel>> GetByAreaAsync(
            PublicPropertyAreaSearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyDetailReadModel?> GetDetailAsync(
            int propertyId,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyDetailReadModel?> GetEnrichedDetailAsync(
            int propertyId,
            DateTimeOffset asOfUtc,
            DateOnly currentMonthStart,
            CancellationToken cancellationToken)
        {
            CurrentMonthStart = currentMonthStart;
            return Task.FromResult<PublicPropertyDetailReadModel?>(null);
        }

        public Task<PublicPropertyRoomPageReadModel?> GetRoomsAsync(
            int propertyId,
            PublicPropertySearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<List<PublicPropertyRoomTypeReadModel>?> GetRoomTypesAsync(
            int propertyId,
            PublicPropertySearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(RoomTypesToReturn);

        public Task<PublicRoomDetailReadModel?> GetRoomDetailAsync(
            int roomId,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
