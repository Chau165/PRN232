using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
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

    private sealed class CapturingPublicPropertyListingRepository : IPublicPropertyListingRepository
    {
        public DateOnly? CurrentMonthStart { get; private set; }

        public Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyPageDto> GetPageAsync(
            PublicPropertySearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PublicPropertyAreaCardDto>> GetByAreaAsync(
            PublicPropertyAreaSearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyDetailDto?> GetDetailAsync(
            int propertyId,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicPropertyDetailDto?> GetEnrichedDetailAsync(
            int propertyId,
            DateTimeOffset asOfUtc,
            DateOnly currentMonthStart,
            CancellationToken cancellationToken)
        {
            CurrentMonthStart = currentMonthStart;
            return Task.FromResult<PublicPropertyDetailDto?>(null);
        }

        public Task<PublicPropertyRoomPageDto?> GetRoomsAsync(
            int propertyId,
            PublicPropertySearchCriteria criteria,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PublicRoomDetailDto?> GetRoomDetailAsync(
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
