using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ConverterResponseRegressionTests
{
    [Fact]
    public async Task Public_pages_preserve_metadata_order_nulls_and_nested_amenities()
    {
        var repository = new PublicRepository
        {
            Page = new([
                new(9, "First", "Address", "Ward", "District", "City", null,
                    [new(8, "Wi-Fi"), new(2, "Parking")], 3, 1234567.89m),
                new(1, "Second", "Address 2", "Ward 2", "District 2", "City", "image", [], 0, null)
            ], 47, 3, 2),
            Rooms = new([
                new(7, "B-7", 4, "Studio", 2345678.91m, 23.45m, null, [new(8, "Wi-Fi")]),
                new(2, "A-2", 1, "Single", 1000000m, 15m, "room-image", [])
            ], 19, 2, 2)
        };
        var service = new PublicPropertyListingService(repository, TimeProvider.System);

        var page = await service.GetAllAsync(new PublicPropertyQueryDto(), CancellationToken.None);
        var rooms = await service.GetRoomsAsync(9, new PublicPropertyRoomsQueryDto(), CancellationToken.None);

        Assert.Equal((47, 3, 2), (page.TotalCount, page.Page, page.PageSize));
        Assert.Equal(new[] { 9, 1 }, page.Items.Select(item => item.PropertyId));
        Assert.Null(page.Items[0].PrimaryImageUrl);
        Assert.Equal(1234567.89m, page.Items[0].StartingPrice);
        Assert.Equal(new[] { new PublicAmenitySummaryDto(8, "Wi-Fi"), new(2, "Parking") }, page.Items[0].AmenitiesSummary);
        Assert.Empty(page.Items[1].AmenitiesSummary);
        Assert.Null(page.Items[1].StartingPrice);
        Assert.NotNull(rooms);
        Assert.Equal((19, 2, 2), (rooms.TotalCount, rooms.Page, rooms.PageSize));
        Assert.Equal(new[] { 7, 2 }, rooms.Items.Select(item => item.RoomId));
        Assert.Equal(2345678.91m, rooms.Items[0].BaseRent);
        Assert.Equal(23.45m, rooms.Items[0].AreaSqm);
        Assert.Equal(new PublicAmenitySummaryDto(8, "Wi-Fi"), Assert.Single(rooms.Items[0].AmenitiesSummary));
        Assert.Null(rooms.Items[0].PrimaryImageUrl);
        Assert.Empty(rooms.Items[1].AmenitiesSummary);
        Assert.Equal(2, repository.ReadCount);
    }

    [Fact]
    public async Task Public_detail_preserves_enrichment_rates_and_nullable_effective_end()
    {
        var start = new DateOnly(2026, 11, 1);
        var repository = new PublicRepository
        {
            Detail = new(7, "Home", "Address", "Ward", "District", "City", null, 4, 1234567.89m,
                [new(8, "Wi-Fi", 3), new(2, "Parking", 1)], "Description",
                [new(6, "Lift"), new(1, "Garden")],
                [new(9, "Water", 12345.67m, "m3", "usage", start, null),
                 new(2, "Internet", 99000m, "room", "fixed_room", start, start.AddMonths(1))])
        };
        var service = new PublicPropertyListingService(repository, TimeProvider.System, propertyDetailEnrichmentEnabled: true);

        var detail = await service.GetDetailAsync(7, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Description", detail.Description);
        Assert.Null(detail.PrimaryImageUrl);
        Assert.Equal(new[] { new PublicPropertyRoomAmenityDto(8, "Wi-Fi", 3), new(2, "Parking", 1) }, detail.RoomAmenities);
        Assert.Equal(new[] { new PublicAmenitySummaryDto(6, "Lift"), new(1, "Garden") }, detail.CommonAmenities);
        Assert.Equal(new[] {
            new PublicPropertyServiceRateDto(9, "Water", 12345.67m, "m3", "usage", start, null),
            new(2, "Internet", 99000m, "room", "fixed_room", start, start.AddMonths(1))
        }, detail.LivingCosts);
        Assert.Equal(1, repository.ReadCount);
    }

    [Fact]
    public async Task Public_detail_keeps_optional_collections_empty_and_missing_responses_null()
    {
        var repository = new PublicRepository { Detail = new(7, "Home", "Address", "Ward", "District", "City", null, 0, null, []) };
        var service = new PublicPropertyListingService(repository, TimeProvider.System);

        var detail = await service.GetDetailAsync(7, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Null(detail.Description);
        Assert.Null(detail.StartingPrice);
        Assert.Empty(detail.RoomAmenities);
        Assert.Empty(detail.CommonAmenities);
        Assert.Empty(detail.LivingCosts);
        repository.Detail = null;
        Assert.Null(await service.GetDetailAsync(7, CancellationToken.None));
        Assert.Null(await service.GetRoomsAsync(7, new PublicPropertyRoomsQueryDto(), CancellationToken.None));
        Assert.Null(await service.GetRoomDetailAsync(7, CancellationToken.None));
    }

    [Fact]
    public async Task Public_room_detail_preserves_all_nested_objects_and_collection_order()
    {
        var repository = new PublicRepository
        {
            RoomDetail = new(7, "B-7", 23.45m, new(4, "Studio"), 2345678.91m,
                new(9, "Home", "Address", "Ward", "District", "City"),
                ["second-image", "first-image"], [new(8, "Wi-Fi"), new(2, "Parking")],
                [new(6, "Water", 12345.67m), new(1, "Electricity", 3456.78m)],
                [new(5, "Bed"), new(2, "Desk")])
        };
        var service = new PublicPropertyListingService(repository, TimeProvider.System);

        var detail = await service.GetRoomDetailAsync(7, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal((7, "B-7", 23.45m, 2345678.91m), (detail.RoomId, detail.RoomCode, detail.AreaSqm, detail.BaseRent));
        Assert.Equal(new PublicRoomTypeDto(4, "Studio"), detail.RoomType);
        Assert.Equal(new PublicRoomPropertyDto(9, "Home", "Address", "Ward", "District", "City"), detail.Property);
        Assert.Equal(new[] { "second-image", "first-image" }, detail.Images);
        Assert.Equal(new[] { new PublicAmenitySummaryDto(8, "Wi-Fi"), new(2, "Parking") }, detail.Amenities);
        Assert.Equal(new[] { new PublicRoomServiceDto(6, "Water", 12345.67m), new(1, "Electricity", 3456.78m) }, detail.Services);
        Assert.Equal(new[] { new PublicRoomEquipmentDto(5, "Bed"), new(2, "Desk") }, detail.Equipments);
        Assert.Equal(1, repository.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rental_responses_preserve_nested_order_prices_nulls_and_empty_collections(bool history)
    {
        var first = new RentalReadModel(9, "Active", new(2026, 1, 1), new(2027, 1, 1),
            2345678.91m, 3456789.12m, 7, "B-7", "Occupied", 23.45m, 4, "Studio", 3, "Home", "Address",
            null, ["second-image", "first-image"], [new(8, "Bed", "Good"), new(2, "Desk", "Used")],
            [new(6, "Wi-Fi"), new(1, "Parking")]);
        var second = first with { ContractId = 1, Images = [], Equipments = [], Amenities = [], PrimaryImageUrl = "primary" };
        var repository = new RentalRepository([first, second]);
        var service = new RentalService(repository, new CurrentCustomer(), TimeProvider.System);

        var result = history
            ? await service.GetRentalHistoryAsync(CancellationToken.None)
            : await service.GetCurrentRentalsAsync(CancellationToken.None);

        Assert.Equal(new[] { 9, 1 }, result.Select(item => item.ContractId));
        Assert.Equal((2345678.91m, 3456789.12m, 23.45m), (result[0].MonthlyRent, result[0].DepositAmount, result[0].AreaSqm));
        Assert.Null(result[0].PrimaryImageUrl);
        Assert.Equal(new[] { "second-image", "first-image" }, result[0].Images);
        Assert.Equal(new[] { new RentalEquipmentDto(8, "Bed", "Good"), new(2, "Desk", "Used") }, result[0].Equipments);
        Assert.Equal(new[] { new RentalAmenityDto(6, "Wi-Fi"), new(1, "Parking") }, result[0].Amenities);
        Assert.Equal("primary", result[1].PrimaryImageUrl);
        Assert.Empty(result[1].Images);
        Assert.Empty(result[1].Equipments);
        Assert.Empty(result[1].Amenities);
        Assert.Equal(1, repository.ReadCount);
    }

    private sealed class PublicRepository : IPublicPropertyListingRepository
    {
        public PublicPropertyPageReadModel Page { get; init; } = new([], 0, 1, 10);
        public PublicPropertyRoomPageReadModel? Rooms { get; init; }
        public PublicPropertyDetailReadModel? Detail { get; set; }
        public PublicRoomDetailReadModel? RoomDetail { get; init; }
        public int ReadCount { get; private set; }

        public Task<bool> AmenityIdsExistAsync(int[] amenityIds, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<PublicPropertyPageReadModel> GetPageAsync(PublicPropertySearchCriteria criteria, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Page);
        }

        public Task<IReadOnlyList<PublicPropertyAreaCardReadModel>> GetByAreaAsync(PublicPropertyAreaSearchCriteria criteria, DateTimeOffset asOfUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PublicPropertyDetailReadModel?> GetDetailAsync(int propertyId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Detail);
        }

        public Task<PublicPropertyDetailReadModel?> GetEnrichedDetailAsync(int propertyId, DateTimeOffset asOfUtc, DateOnly currentMonthStart, CancellationToken cancellationToken) => GetDetailAsync(propertyId, asOfUtc, cancellationToken);

        public Task<PublicPropertyRoomPageReadModel?> GetRoomsAsync(int propertyId, PublicPropertySearchCriteria criteria, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Rooms);
        }

        public Task<PublicRoomDetailReadModel?> GetRoomDetailAsync(int roomId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(RoomDetail);
        }
    }

    private sealed class RentalRepository(IReadOnlyList<RentalReadModel> rentals) : IRentalRepository
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<RentalReadModel>> GetCurrentRentalsAsync(int customerId, DateOnly today, CancellationToken cancellationToken)
        {
            Assert.Equal(42, customerId);
            ReadCount++;
            return Task.FromResult(rentals);
        }

        public Task<IReadOnlyList<RentalReadModel>> GetRentalHistoryAsync(int customerId, DateOnly today, CancellationToken cancellationToken) => GetCurrentRentalsAsync(customerId, today, cancellationToken);
    }

    private sealed class CurrentCustomer : ICurrentUserService
    {
        public int? UserId => 42;
        public string? Email => null;
        public string? Role => RoleNames.Customer;
        public string? FullName => null;
        public bool IsAuthenticated => true;
    }
}
