using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Api.Controllers;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PublicPropertyListingApiTests
{
    private static readonly DateTimeOffset AsOfUtc = new(2026, 10, 19, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Anonymous_listing_returns_only_allowlisted_property_cards()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var document = await GetListingAsync(client);

        Assert.Equal(3, document.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { 1, 2, 5 }, GetPropertyIds(document));

        var card = GetItem(document, 1);
        Assert.Equal(
            new[]
            {
                "addressText", "amenitiesSummary", "availableRoomCount", "city", "district",
                "primaryImageUrl", "propertyId", "propertyName", "startingPrice", "ward"
            },
            card.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal("North_Star 100% Residence", card.GetProperty("propertyName").GetString());
        Assert.False(card.TryGetProperty("ownerId", out _));
        Assert.False(card.TryGetProperty("ownerName", out _));
        Assert.False(card.TryGetProperty("status", out _));
        Assert.False(card.TryGetProperty("bookings", out _));
        Assert.Equal(1, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(10, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, factory.Clock.ReadCount);
    }

    [Fact]
    public async Task Swagger_lists_public_listing_without_bearer_security()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/public/properties")
            .GetProperty("get");

        Assert.False(operation.TryGetProperty("security", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
    }

    [Fact]
    public async Task Listing_uses_phase2a_blockers_and_aggregates_only_matching_rentable_rooms()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var document = await GetListingAsync(client);

        var first = GetItem(document, 1);
        Assert.Equal(3, first.GetProperty("availableRoomCount").GetInt32());
        Assert.Equal(1_500_000m, first.GetProperty("startingPrice").GetDecimal());

        var second = GetItem(document, 2);
        Assert.Equal(1, second.GetProperty("availableRoomCount").GetInt32());
        Assert.Equal(4_000_000m, second.GetProperty("startingPrice").GetDecimal());
        Assert.DoesNotContain(3, GetPropertyIds(document));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Available", (await context.Rooms.SingleAsync(room => room.RoomId == 102)).Status);
        Assert.Equal("Deposited", (await context.Bookings.SingleAsync(booking => booking.RoomId == 100)).Status);
        Assert.Equal("Active", (await context.Contracts.SingleAsync(contract => contract.RoomId == 202)).Status);
    }

    [Fact]
    public async Task Room_type_and_amenity_filters_apply_to_one_room_and_keep_same_name_type_ids()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var sameNameTypes = await GetListingAsync(client, "?roomTypeName=%20sTuDiO%20");
        var studioCard = GetItem(sameNameTypes, 1);
        Assert.Equal(2, studioCard.GetProperty("availableRoomCount").GetInt32());
        Assert.Equal(1_500_000m, studioCard.GetProperty("startingPrice").GetDecimal());

        using var sameRoomRequired = await GetListingAsync(client, "?minPrice=3500000&amenityIds=1");
        Assert.Equal(new[] { 2 }, GetPropertyIds(sameRoomRequired));
        Assert.Equal(1, GetItem(sameRoomRequired, 2).GetProperty("availableRoomCount").GetInt32());

        using var duplicateAmenity = await GetListingAsync(client, "?roomTypeName=loft&amenityIds=1&amenityIds=2&amenityIds=1");
        Assert.Equal(new[] { 2 }, GetPropertyIds(duplicateAmenity));
        Assert.Equal(1, GetItem(duplicateAmenity, 2).GetProperty("availableRoomCount").GetInt32());

        using var everySameNameType = await GetListingAsync(client, "?roomTypeName=loft");
        Assert.Equal(new[] { 2, 5 }, GetPropertyIds(everySameNameType));

        using var inclusivePriceInterval = await GetListingAsync(client, "?minPrice=4000000&maxPrice=4000000");
        Assert.Equal(new[] { 1, 2, 5 }, GetPropertyIds(inclusivePriceInterval));
        Assert.All(inclusivePriceInterval.RootElement.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal(1, item.GetProperty("availableRoomCount").GetInt32());
            Assert.Equal(4_000_000m, item.GetProperty("startingPrice").GetDecimal());
        });

        using var combinedFilters = await GetListingAsync(
            client,
            "?minPrice=3500000&roomTypeName=loft&amenityIds=1&amenityIds=2");
        Assert.Equal(new[] { 2 }, GetPropertyIds(combinedFilters));
    }

    [Fact]
    public async Task Keyword_is_case_insensitive_and_treats_like_wildcards_as_literals()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var underscore = await GetListingAsync(client, "?keyword=nOrTh_StAr");
        Assert.Equal(new[] { 1 }, GetPropertyIds(underscore));

        using var percent = await GetListingAsync(client, "?keyword=100%25");
        Assert.Equal(new[] { 1 }, GetPropertyIds(percent));

        using var city = await GetListingAsync(client, "?city=%20tp.hcm%20");
        Assert.Equal(new[] { 1, 2 }, GetPropertyIds(city));

        using var fullLocation = await GetListingAsync(
            client,
            "?city=tp.hcm&district=THU%20DUC&ward=linh%20trung");
        Assert.Equal(new[] { 1 }, GetPropertyIds(fullLocation));
    }

    [Fact]
    public async Task Pagination_and_price_sorts_are_stable_and_total_counts_properties()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var firstPage = await GetListingAsync(client, "?sortBy=price_desc&page=1&pageSize=1");
        Assert.Equal(3, firstPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { 2 }, GetPropertyIds(firstPage));
        Assert.Equal(1, firstPage.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, firstPage.RootElement.GetProperty("pageSize").GetInt32());

        using var secondPage = await GetListingAsync(client, "?sortBy=price_desc&page=2&pageSize=1");
        Assert.Equal(new[] { 5 }, GetPropertyIds(secondPage));

        using var ascending = await GetListingAsync(client, "?sortBy=price_asc");
        Assert.Equal(new[] { 1, 2, 5 }, GetPropertyIds(ascending));

        using var emptyPage = await GetListingAsync(client, "?page=4&pageSize=1");
        Assert.Empty(emptyPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(3, emptyPage.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Representative_room_amenities_are_bounded_and_unverified_images_are_not_exposed()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var document = await GetListingAsync(client);

        var card = GetItem(document, 2);
        var amenities = card.GetProperty("amenitiesSummary").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, amenities.Select(item => item.GetProperty("amenityId").GetInt32()));
        Assert.All(amenities, amenity => Assert.True(amenity.TryGetProperty("name", out _)));
        Assert.Null(card.GetProperty("primaryImageUrl").GetString());
        Assert.DoesNotContain("private-token", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=51")]
    [InlineData("?minPrice=-1")]
    [InlineData("?maxPrice=-1")]
    [InlineData("?minPrice=4000000&maxPrice=3000000")]
    [InlineData("?page=50000000&pageSize=50")]
    [InlineData("?amenityIds=0")]
    [InlineData("?amenityIds=999")]
    [InlineData("?sortBy=recent")]
    [InlineData("?minPrice=not-a-number")]
    public async Task Invalid_listing_parameters_return_a_validation_problem(string query)
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/properties" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task No_matching_rentable_room_returns_an_empty_success_page()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var document = await GetListingAsync(client, "?minPrice=100000000");

        Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Anonymous_property_detail_returns_allowlisted_phase2a_aggregates_without_mutating_state()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/properties/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = document.RootElement;
        Assert.Equal(
            new[] { "addressText", "availableRoomCount", "city", "commonAmenities", "description", "district", "livingCosts", "primaryImageUrl", "propertyId", "propertyName", "roomAmenities", "startingPrice", "ward" },
            detail.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(1, detail.GetProperty("propertyId").GetInt32());
        Assert.Equal("North_Star 100% Residence", detail.GetProperty("propertyName").GetString());
        Assert.Equal(3, detail.GetProperty("availableRoomCount").GetInt32());
        Assert.Equal(1_500_000m, detail.GetProperty("startingPrice").GetDecimal());
        Assert.Null(detail.GetProperty("primaryImageUrl").GetString());
        Assert.Null(detail.GetProperty("description").GetString());
        Assert.Empty(detail.GetProperty("commonAmenities").EnumerateArray());
        Assert.Empty(detail.GetProperty("livingCosts").EnumerateArray());
        Assert.DoesNotContain("Private Owner Name", detail.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private Tenant Name", detail.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(1, factory.Clock.ReadCount);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Available", (await context.Rooms.SingleAsync(room => room.RoomId == 100)).Status);
        Assert.Equal("Deposited", (await context.Bookings.SingleAsync(booking => booking.RoomId == 100)).Status);
        Assert.Equal("Active", (await context.Contracts.SingleAsync(contract => contract.RoomId == 202)).Status);
    }

    [Fact]
    public async Task Property_detail_includes_only_amenities_from_its_rentable_rooms()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var firstResponse = await client.GetAsync("/api/public/properties/1");
        using var firstDocument = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstAmenities = firstDocument.RootElement.GetProperty("roomAmenities").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, firstAmenities.Select(amenity => amenity.GetProperty("amenityId").GetInt32()));
        Assert.All(firstAmenities, amenity => Assert.Equal(1, amenity.GetProperty("rentableRoomCount").GetInt32()));
        Assert.DoesNotContain(firstAmenities, amenity => amenity.GetProperty("name").GetString() == "Parking");
        Assert.Null(firstDocument.RootElement.GetProperty("description").GetString());
        Assert.Empty(firstDocument.RootElement.GetProperty("commonAmenities").EnumerateArray());
        Assert.Empty(firstDocument.RootElement.GetProperty("livingCosts").EnumerateArray());

        using var secondResponse = await client.GetAsync("/api/public/properties/2");
        using var secondDocument = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondAmenities = secondDocument.RootElement.GetProperty("roomAmenities").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, secondAmenities.Select(amenity => amenity.GetProperty("amenityId").GetInt32()));
        Assert.All(secondAmenities, amenity => Assert.Equal(1, amenity.GetProperty("rentableRoomCount").GetInt32()));
    }

    [Fact]
    public async Task Enriched_property_detail_uses_property_scoped_amenities_and_vietnamese_effective_date()
    {
        using var octoberFactory = new PublicPropertiesApiFactory(
            propertyDetailEnrichmentEnabled: true,
            utcNow: new DateTimeOffset(2026, 10, 31, 16, 59, 0, TimeSpan.Zero));
        using var octoberClient = octoberFactory.CreateClient();

        using var firstResponse = await octoberClient.GetAsync("/api/public/properties/1");
        using var secondResponse = await octoberClient.GetAsync("/api/public/properties/2");
        using var emptyResponse = await octoberClient.GetAsync("/api/public/properties/5");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);

        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        using var empty = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync());
        var firstDetail = first.RootElement;
        Assert.Equal("Verified Alpha description", firstDetail.GetProperty("description").GetString());
        Assert.Equal(new[] { 3 }, firstDetail.GetProperty("commonAmenities").EnumerateArray()
            .Select(item => item.GetProperty("amenityId").GetInt32()));
        Assert.Equal(new[] { 1, 2 }, firstDetail.GetProperty("roomAmenities").EnumerateArray()
            .Select(item => item.GetProperty("amenityId").GetInt32()));
        Assert.DoesNotContain(firstDetail.GetProperty("roomAmenities").EnumerateArray(),
            item => item.GetProperty("amenityId").GetInt32() == 3);

        var firstRate = Assert.Single(firstDetail.GetProperty("livingCosts").EnumerateArray());
        Assert.Equal(11, firstRate.GetProperty("serviceId").GetInt32());
        Assert.Equal("Internet", firstRate.GetProperty("serviceName").GetString());
        Assert.Equal(120_000m, firstRate.GetProperty("price").GetDecimal());
        Assert.Equal("room_month", firstRate.GetProperty("unitCode").GetString());
        Assert.Equal("fixed_room", firstRate.GetProperty("billingBasis").GetString());
        Assert.Equal("2026-10-01", firstRate.GetProperty("effectiveFrom").GetString());
        Assert.Equal("2026-11-01", firstRate.GetProperty("effectiveTo").GetString());

        var secondRate = Assert.Single(second.RootElement.GetProperty("livingCosts").EnumerateArray());
        Assert.Equal(180_000m, secondRate.GetProperty("price").GetDecimal());
        Assert.Null(empty.RootElement.GetProperty("description").GetString());
        Assert.Empty(empty.RootElement.GetProperty("commonAmenities").EnumerateArray());
        Assert.Empty(empty.RootElement.GetProperty("roomAmenities").EnumerateArray());
        Assert.Empty(empty.RootElement.GetProperty("livingCosts").EnumerateArray());

        using var novemberFactory = new PublicPropertiesApiFactory(
            propertyDetailEnrichmentEnabled: true,
            utcNow: new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero));
        using var novemberClient = novemberFactory.CreateClient();
        using var novemberResponse = await novemberClient.GetAsync("/api/public/properties/1");
        Assert.Equal(HttpStatusCode.OK, novemberResponse.StatusCode);
        using var november = JsonDocument.Parse(await novemberResponse.Content.ReadAsStringAsync());
        var novemberRate = Assert.Single(november.RootElement.GetProperty("livingCosts").EnumerateArray());
        Assert.Equal(150_000m, novemberRate.GetProperty("price").GetDecimal());
        Assert.Equal("2026-11-01", novemberRate.GetProperty("effectiveFrom").GetString());
    }

    [Fact]
    public async Task Development_configuration_enables_enriched_property_detail_fields()
    {
        using var factory = new PublicPropertiesApiFactory(
            propertyDetailEnrichmentEnabled: null,
            environment: "Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/properties/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Verified Alpha description", detail.RootElement.GetProperty("description").GetString());
        Assert.Equal(new[] { 3 }, detail.RootElement.GetProperty("commonAmenities").EnumerateArray()
            .Select(amenity => amenity.GetProperty("amenityId").GetInt32()));
    }

    [Fact]
    public async Task Unknown_and_ineligible_property_details_return_the_same_not_found_contract()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var ineligibleResponse = await client.GetAsync("/api/public/properties/3");
        using var unknownResponse = await client.GetAsync("/api/public/properties/999");

        Assert.Equal(HttpStatusCode.NotFound, ineligibleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        var ineligibleBody = await ineligibleResponse.Content.ReadAsStringAsync();
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        using var ineligibleProblem = JsonDocument.Parse(ineligibleBody);
        using var unknownProblem = JsonDocument.Parse(unknownBody);
        Assert.Equal(404, ineligibleProblem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(404, unknownProblem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            ineligibleProblem.RootElement.GetProperty("type").GetString(),
            unknownProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            ineligibleProblem.RootElement.GetProperty("title").GetString(),
            unknownProblem.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Unavailable House", ineligibleBody, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnerId", ineligibleBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_property_room_list_returns_only_rentable_scoped_rooms_and_allowlisted_fields()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/properties/1/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var page = document.RootElement;
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
        var rooms = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { 102, 103, 104 }, rooms.Select(room => room.GetProperty("roomId").GetInt32()));
        Assert.Equal(
            new[] { "amenitiesSummary", "areaSqm", "baseRent", "primaryImageUrl", "roomCode", "roomId", "roomTypeId", "roomTypeName" },
            rooms[0].EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal("R102", rooms[0].GetProperty("roomCode").GetString());
        Assert.Equal(102, rooms[0].GetProperty("roomTypeId").GetInt32());
        Assert.Equal(3_000_000m, rooms[0].GetProperty("baseRent").GetDecimal());
        Assert.All(rooms, room => Assert.Null(room.GetProperty("primaryImageUrl").GetString()));
        Assert.DoesNotContain("private-token", page.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("status", string.Join(',', rooms.SelectMany(room => room.EnumerateObject().Select(property => property.Name))), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, factory.Clock.ReadCount);
    }

    [Fact]
    public async Task Room_filters_match_one_room_and_logical_type_name_keeps_distinct_type_ids()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var sameName = await client.GetAsync("/api/public/properties/1/rooms?roomTypeName=%20sTuDiO%20");
        Assert.Equal(HttpStatusCode.OK, sameName.StatusCode);
        using var studioPage = JsonDocument.Parse(await sameName.Content.ReadAsStringAsync());
        var studios = studioPage.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { 102, 104 }, studios.Select(room => room.GetProperty("roomId").GetInt32()));
        Assert.Equal(new[] { 102, 104 }, studios.Select(room => room.GetProperty("roomTypeId").GetInt32()));
        Assert.Equal(new[] { 3_000_000m, 1_500_000m }, studios.Select(room => room.GetProperty("baseRent").GetDecimal()));

        using var amenitiesFromDifferentRooms = await client.GetAsync("/api/public/properties/1/rooms?amenityIds=1&amenityIds=2");
        Assert.Equal(HttpStatusCode.OK, amenitiesFromDifferentRooms.StatusCode);
        using var emptyPage = JsonDocument.Parse(await amenitiesFromDifferentRooms.Content.ReadAsStringAsync());
        Assert.Empty(emptyPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, emptyPage.RootElement.GetProperty("totalCount").GetInt32());

        using var combinedRoomFilters = await client.GetAsync(
            "/api/public/properties/2/rooms?roomTypeName=loft&minPrice=4000000&maxPrice=4000000&amenityIds=1&amenityIds=2&amenityIds=1");
        Assert.Equal(HttpStatusCode.OK, combinedRoomFilters.StatusCode);
        using var combinedPage = JsonDocument.Parse(await combinedRoomFilters.Content.ReadAsStringAsync());
        var matchingRoom = Assert.Single(combinedPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(201, matchingRoom.GetProperty("roomId").GetInt32());
        Assert.Equal(4_000_000m, matchingRoom.GetProperty("baseRent").GetDecimal());
    }

    [Fact]
    public async Task Room_sorting_and_pagination_are_stable_with_room_id_tie_break()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.RoomTypes.Add(new RoomTypes { RoomTypeId = 105, TypeName = "Studio", Price = 3_000_000m });
            context.Rooms.Add(new Rooms
            {
                RoomId = 105,
                PropertyId = 1,
                RoomTypeId = 105,
                RoomCode = "R105",
                AreaSqm = 21m,
                Status = "Available"
            });
            await context.SaveChangesAsync();
        }

        using var tiePage = await client.GetAsync("/api/public/properties/1/rooms?sortBy=price_asc&page=2&pageSize=1");
        using var nextTiePage = await client.GetAsync("/api/public/properties/1/rooms?sortBy=price_asc&page=3&pageSize=1");
        using var descending = await client.GetAsync("/api/public/properties/1/rooms?sortBy=price_desc");
        Assert.Equal(HttpStatusCode.OK, tiePage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nextTiePage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, descending.StatusCode);
        using var tie = JsonDocument.Parse(await tiePage.Content.ReadAsStringAsync());
        using var nextTie = JsonDocument.Parse(await nextTiePage.Content.ReadAsStringAsync());
        using var descendingPage = JsonDocument.Parse(await descending.Content.ReadAsStringAsync());
        Assert.Equal(4, tie.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(102, Assert.Single(tie.RootElement.GetProperty("items").EnumerateArray()).GetProperty("roomId").GetInt32());
        Assert.Equal(105, Assert.Single(nextTie.RootElement.GetProperty("items").EnumerateArray()).GetProperty("roomId").GetInt32());
        Assert.Equal(new[] { 103, 102, 105, 104 }, descendingPage.RootElement.GetProperty("items").EnumerateArray().Select(room => room.GetProperty("roomId").GetInt32()));
    }

    [Fact]
    public async Task Empty_filtered_or_out_of_range_room_page_is_success_for_visible_property()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var filteredEmpty = await client.GetAsync("/api/public/properties/1/rooms?minPrice=100000000");
        using var outOfRange = await client.GetAsync("/api/public/properties/1/rooms?page=4&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, filteredEmpty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outOfRange.StatusCode);
        using var filteredPage = JsonDocument.Parse(await filteredEmpty.Content.ReadAsStringAsync());
        using var outOfRangePage = JsonDocument.Parse(await outOfRange.Content.ReadAsStringAsync());
        Assert.Empty(filteredPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, filteredPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Empty(outOfRangePage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(3, outOfRangePage.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=51")]
    [InlineData("?page=50000000&pageSize=50")]
    [InlineData("?minPrice=-1")]
    [InlineData("?maxPrice=-1")]
    [InlineData("?minPrice=4000000&maxPrice=3000000")]
    [InlineData("?minPrice=not-a-number")]
    [InlineData("?amenityIds=0")]
    [InlineData("?amenityIds=999")]
    [InlineData("?sortBy=price")]
    [InlineData("?sortBy=propertyId")]
    public async Task Invalid_room_list_query_returns_validation_problem(string query)
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/properties/1/rooms" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
        Assert.DoesNotContain("OwnerId", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CustomerId", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Room_list_uses_route_property_id_and_404s_for_unknown_or_ineligible_properties()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var scoped = await client.GetAsync("/api/public/properties/1/rooms?propertyId=2");
        using var unknown = await client.GetAsync("/api/public/properties/999/rooms");
        using var ineligible = await client.GetAsync("/api/public/properties/3/rooms");

        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ineligible.StatusCode);
        using var scopedPage = JsonDocument.Parse(await scoped.Content.ReadAsStringAsync());
        Assert.Equal(new[] { 102, 103, 104 }, scopedPage.RootElement.GetProperty("items").EnumerateArray().Select(room => room.GetProperty("roomId").GetInt32()));
    }

    [Fact]
    public async Task Property_that_loses_all_rentable_rooms_between_home_and_detail_returns_404()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var home = await GetListingAsync(client);
        Assert.Contains(1, GetPropertyIds(home));
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rooms = await context.Rooms.Where(room => room.PropertyId == 1).ToListAsync();
            foreach (var room in rooms)
            {
                room.Status = "Occupied";
            }
            await context.SaveChangesAsync();
        }

        using var detail = await client.GetAsync("/api/public/properties/1");
        using var roomList = await client.GetAsync("/api/public/properties/1/rooms");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, roomList.StatusCode);
    }

    [Fact]
    public async Task Swagger_lists_both_public_property_routes_without_bearer_security()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        AssertAnonymousOperation(paths.GetProperty("/api/public/properties/{id}").GetProperty("get"), includeValidation: false);
        AssertAnonymousOperation(paths.GetProperty("/api/public/properties/{id}/rooms").GetProperty("get"), includeValidation: true);
    }

    [Fact]
    public async Task Anonymous_room_detail_returns_an_allowlisted_snapshot_without_mutating_room_state()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/rooms/102");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = document.RootElement;
        Assert.Equal(
            new[] { "amenities", "areaSqm", "baseRent", "equipments", "images", "property", "roomCode", "roomId", "roomType", "services" },
            detail.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(102, detail.GetProperty("roomId").GetInt32());
        Assert.Equal("R102", detail.GetProperty("roomCode").GetString());
        Assert.Equal(3_000_000m, detail.GetProperty("baseRent").GetDecimal());
        Assert.Equal(102, detail.GetProperty("roomType").GetProperty("roomTypeId").GetInt32());
        Assert.Equal("Studio", detail.GetProperty("roomType").GetProperty("roomTypeName").GetString());
        Assert.Equal(
            new[] { "roomTypeId", "roomTypeName" },
            detail.GetProperty("roomType").EnumerateObject().Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var property = detail.GetProperty("property");
        Assert.Equal(1, property.GetProperty("propertyId").GetInt32());
        Assert.Equal("North_Star 100% Residence", property.GetProperty("propertyName").GetString());
        Assert.Equal(
            new[] { "addressText", "city", "district", "propertyId", "propertyName", "ward" },
            property.EnumerateObject().Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var amenity = Assert.Single(detail.GetProperty("amenities").EnumerateArray());
        Assert.Equal(1, amenity.GetProperty("amenityId").GetInt32());
        Assert.Equal(new[] { "amenityId", "name" },
            amenity.EnumerateObject().Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        var service = Assert.Single(detail.GetProperty("services").EnumerateArray());
        Assert.Equal(11, service.GetProperty("serviceId").GetInt32());
        Assert.Equal("Internet", service.GetProperty("name").GetString());
        Assert.Equal(100_000m, service.GetProperty("price").GetDecimal());
        Assert.Equal(new[] { "name", "price", "serviceId" },
            service.EnumerateObject().Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Empty(detail.GetProperty("images").EnumerateArray());
        Assert.Empty(detail.GetProperty("equipments").EnumerateArray());
        Assert.DoesNotContain("private-token", detail.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private Owner Name", detail.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private Tenant Name", detail.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("supplier", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("warranty", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"status\"", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"ownerId\"", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"customerId\"", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status", string.Join(',', detail.EnumerateObject().Select(item => item.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, factory.Clock.ReadCount);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Available", (await context.Rooms.SingleAsync(room => room.RoomId == 102)).Status);
        Assert.Equal("Cancelled", (await context.Bookings.FirstAsync(booking => booking.RoomId == 102)).Status);
    }

    [Fact]
    public async Task Room_detail_uses_exact_room_type_id_for_base_rent_and_services()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var firstResponse = await client.GetAsync("/api/public/rooms/102");
        using var secondResponse = await client.GetAsync("/api/public/rooms/104");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(102, first.RootElement.GetProperty("roomType").GetProperty("roomTypeId").GetInt32());
        Assert.Equal(1_500_000m, second.RootElement.GetProperty("baseRent").GetDecimal());
        Assert.Equal(104, second.RootElement.GetProperty("roomType").GetProperty("roomTypeId").GetInt32());
        Assert.Equal("Studio", second.RootElement.GetProperty("roomType").GetProperty("roomTypeName").GetString());
        Assert.Equal(11, Assert.Single(first.RootElement.GetProperty("services").EnumerateArray())
            .GetProperty("serviceId").GetInt32());
        Assert.Equal(12, Assert.Single(second.RootElement.GetProperty("services").EnumerateArray())
            .GetProperty("serviceId").GetInt32());
        Assert.Equal(2, factory.Clock.ReadCount);
    }

    [Fact]
    public async Task Room_detail_amenities_are_scoped_and_missing_amenities_or_services_are_empty_arrays()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();

        using var roomWithDifferentAmenity = await client.GetAsync("/api/public/rooms/103");
        using var roomWithoutAmenities = await client.GetAsync("/api/public/rooms/104");

        Assert.Equal(HttpStatusCode.OK, roomWithDifferentAmenity.StatusCode);
        Assert.Equal(HttpStatusCode.OK, roomWithoutAmenities.StatusCode);
        using var first = JsonDocument.Parse(await roomWithDifferentAmenity.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await roomWithoutAmenities.Content.ReadAsStringAsync());
        Assert.Equal(new[] { 2 }, first.RootElement.GetProperty("amenities").EnumerateArray()
            .Select(item => item.GetProperty("amenityId").GetInt32()));
        Assert.Empty(first.RootElement.GetProperty("services").EnumerateArray());
        Assert.Empty(second.RootElement.GetProperty("amenities").EnumerateArray());
    }

    [Fact]
    public async Task Unknown_and_non_rentable_rooms_return_the_same_generic_not_found_contract()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        var unknownBody = await GetNotFoundBodyAsync(client, 999);
        using var invalidRouteValue = await client.GetAsync("/api/public/rooms/not-an-integer");
        Assert.Equal(HttpStatusCode.NotFound, invalidRouteValue.StatusCode);

        foreach (var roomId in new[] { 0, -1, 100, 101, 202, 203, 204, 205, 206, 301, 302 })
        {
            var blockedBody = await GetNotFoundBodyAsync(client, roomId);
            using var unknown = JsonDocument.Parse(unknownBody);
            using var blocked = JsonDocument.Parse(blockedBody);
            Assert.Equal(404, blocked.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(unknown.RootElement.GetProperty("type").GetString(), blocked.RootElement.GetProperty("type").GetString());
            Assert.Equal(unknown.RootElement.GetProperty("title").GetString(), blocked.RootElement.GetProperty("title").GetString());
            Assert.DoesNotContain("owner", blockedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("booking", blockedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("contract", blockedBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Expired_pending_and_cancelled_bookings_do_not_block_but_active_past_contract_does()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.Add(new Contracts
            {
                ContractId = 99,
                RoomId = 201,
                CustomerId = 2,
                Status = "Active",
                StartDate = DateOnly.FromDateTime(AsOfUtc.AddDays(-60).UtcDateTime),
                EndDate = DateOnly.FromDateTime(AsOfUtc.AddDays(-30).UtcDateTime)
            });
            await context.SaveChangesAsync();
        }

        using var cancelledBooking = await client.GetAsync("/api/public/rooms/102");
        using var exactExpiryPending = await client.GetAsync("/api/public/rooms/104");
        using var expiredActiveContract = await client.GetAsync("/api/public/rooms/201");

        Assert.Equal(HttpStatusCode.OK, cancelledBooking.StatusCode);
        Assert.Equal(HttpStatusCode.OK, exactExpiryPending.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expiredActiveContract.StatusCode);
    }

    [Fact]
    public async Task Room_detail_uses_the_room_property_and_swagger_exposes_anonymous_route_without_legacy_collision()
    {
        using var factory = new PublicPropertiesApiFactory();
        using var client = factory.CreateClient();
        using var roomResponse = await client.GetAsync("/api/public/rooms/201");
        Assert.Equal(HttpStatusCode.OK, roomResponse.StatusCode);
        using var room = JsonDocument.Parse(await roomResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, room.RootElement.GetProperty("property").GetProperty("propertyId").GetInt32());
        Assert.Equal(Enumerable.Range(1, 6), room.RootElement.GetProperty("amenities").EnumerateArray()
            .Select(item => item.GetProperty("amenityId").GetInt32()));

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var swagger = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = swagger.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/rooms/{id}", out _));
        AssertAnonymousOperation(paths.GetProperty("/api/public/rooms/{roomId}").GetProperty("get"), includeValidation: false);
    }

    private static async Task<string> GetNotFoundBodyAsync(HttpClient client, int roomId)
    {
        using var response = await client.GetAsync($"/api/public/rooms/{roomId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static void AssertAnonymousOperation(JsonElement operation, bool includeValidation)
    {
        Assert.False(operation.TryGetProperty("security", out _));
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.Equal(includeValidation, responses.TryGetProperty("400", out _));
    }

    private static async Task<JsonDocument> GetListingAsync(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync("/api/public/properties" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static int[] GetPropertyIds(JsonDocument document) =>
        document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("propertyId").GetInt32())
            .ToArray();

    private static JsonElement GetItem(JsonDocument document, int propertyId) =>
        document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("propertyId").GetInt32() == propertyId);

    private sealed class PublicPropertiesApiFactory : WebApplicationFactory<PropertiesController>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();
        private readonly bool? _propertyDetailEnrichmentEnabled;
        private readonly string _environment;
        public FixedTimeProvider Clock { get; }

        public PublicPropertiesApiFactory(
            bool? propertyDetailEnrichmentEnabled = false,
            DateTimeOffset? utcNow = null,
            string environment = "Testing")
        {
            _propertyDetailEnrichmentEnabled = propertyDetailEnrichmentEnabled;
            _environment = environment;
            Clock = new FixedTimeProvider(utcNow ?? AsOfUtc);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var testSettings = new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
                };
                if (_propertyDetailEnrichmentEnabled.HasValue)
                {
                    testSettings["PublicBrowsing:PropertyDetailEnrichmentEnabled"] =
                        _propertyDetailEnrichmentEnabled.Value.ToString();
                }

                configuration.AddInMemoryCollection(testSettings);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
            Seed(context);
        }

        private static void Seed(AppDbContext context)
        {
            context.Roles.Add(new Roles { RoleId = 1, RoleName = "Customer" });
            context.Users.AddRange(
                new Users { UserId = 1, RoleId = 1, FullName = "Private Owner Name", Email = "owner@example.test" },
                new Users { UserId = 2, RoleId = 1, FullName = "Private Tenant Name", Email = "tenant@example.test" });
            context.Properties.AddRange(
                Property(1, "North_Star 100% Residence", "TP.HCM", "Thu Duc", "Linh Trung"),
                Property(2, "River House", "TP.HCM", "Thu Duc", "Binh Tho"),
                Property(3, "Unavailable House", "Ha Noi", "Ba Dinh", "Cong Vi"),
                Property(5, "NorthXStar Residence", "Ha Noi", "Cau Giay", "Dich Vong"));
            context.RoomTypes.AddRange(
                RoomType(100, "Economy", 500_000m),
                RoomType(101, "Economy", 2_000_000m),
                RoomType(102, "Studio", 3_000_000m),
                RoomType(103, "One Bedroom", 4_000_000m),
                RoomType(104, "Studio", 1_500_000m),
                RoomType(201, "Loft", 4_000_000m),
                RoomType(202, "Loft", 3_000_000m),
                RoomType(203, "Economy", 2_000_000m),
                RoomType(204, "Economy", 2_200_000m),
                RoomType(205, "Economy", 2_400_000m),
                RoomType(206, "Economy", 2_500_000m),
                RoomType(301, "Unknown", 1_000_000m),
                RoomType(302, "Unknown", 1_100_000m),
                RoomType(501, "Loft", 4_000_000m));
            context.Rooms.AddRange(
                Room(100, 1, 100, "Available"),
                Room(101, 1, 101, "Available"),
                Room(102, 1, 102, "Available"),
                Room(103, 1, 103, "Available"),
                Room(104, 1, 104, "Available"),
                Room(201, 2, 201, "Available"),
                Room(202, 2, 202, "Available"),
                Room(203, 2, 203, "Reserved"),
                Room(204, 2, 204, "Occupied"),
                Room(205, 2, 205, "Maintenance"),
                Room(206, 2, 206, "FutureRoomStatus"),
                Room(301, 3, 301, "Available"),
                Room(302, 3, 302, "Available"),
                Room(501, 5, 501, "Available"));
            context.Amenities.AddRange(
                new Amenities { AmenityId = 1, Name = "Wi-Fi" },
                new Amenities { AmenityId = 2, Name = "Air conditioning" },
                new Amenities { AmenityId = 3, Name = "Parking" },
                new Amenities { AmenityId = 4, Name = "Elevator" },
                new Amenities { AmenityId = 5, Name = "Security" },
                new Amenities { AmenityId = 6, Name = "Laundry" });
            context.Services.AddRange(
                new Service { ServiceId = 11, Name = "Internet", Price = 100_000m },
                new Service { ServiceId = 12, Name = "Water", Price = 45_000m });
            context.Equipments.AddRange(
                new Equipments
                {
                    EquipmentId = 1,
                    RoomId = 102,
                    Name = "Private equipment name",
                    InstallDate = new DateOnly(2024, 1, 1),
                    WarrantyExpiry = new DateOnly(2027, 1, 1),
                    Supplier = "Private supplier",
                    Status = "Available"
                });
            context.SaveChanges();

            context.Bookings.AddRange(
                Booking(1, 100, "Deposited", AsOfUtc.AddDays(-2)),
                Booking(2, 101, "Pending", AsOfUtc.AddDays(14)),
                Booking(3, 102, "Cancelled", AsOfUtc.AddDays(-1)),
                Booking(4, 102, "Cancelled", AsOfUtc.AddDays(-1)),
                Booking(5, 104, "Pending", AsOfUtc),
                Booking(6, 201, "Cancelled", AsOfUtc.AddDays(-1)),
                Booking(7, 301, "FutureBookingStatus", AsOfUtc.AddDays(1)));
            context.Contracts.AddRange(
                Contract(1, 202, "Active", AsOfUtc.AddDays(30), AsOfUtc.AddDays(60)),
                Contract(2, 302, "FutureContractStatus", AsOfUtc.AddDays(-60), AsOfUtc.AddDays(-30)));
            context.RoomAmenities.AddRange(
                new RoomAmenities { RoomId = 101, AmenityId = 3 },
                new RoomAmenities { RoomId = 102, AmenityId = 1 },
                new RoomAmenities { RoomId = 103, AmenityId = 2 },
                new RoomAmenities { RoomId = 201, AmenityId = 1 },
                new RoomAmenities { RoomId = 201, AmenityId = 2 },
                new RoomAmenities { RoomId = 201, AmenityId = 3 },
                new RoomAmenities { RoomId = 201, AmenityId = 4 },
                new RoomAmenities { RoomId = 201, AmenityId = 5 },
                new RoomAmenities { RoomId = 201, AmenityId = 6 });
            context.RoomTypeServices.AddRange(
                new RoomTypeServices { RoomTypeId = 102, ServiceId = 11 },
                new RoomTypeServices { RoomTypeId = 104, ServiceId = 12 });
            context.RoomImages.AddRange(
                new RoomImages { RoomImageId = 1, RoomId = 102, IsPrimary = true, ImageUrl = "https://storage.example.test/object/sign/room.jpg?token=private-token" },
                new RoomImages { RoomImageId = 2, RoomId = 201, IsPrimary = true, ImageUrl = "https://storage.example.test/object/sign/room.jpg?token=private-token" },
                new RoomImages { RoomImageId = 3, RoomId = 201, IsPrimary = true, ImageUrl = "https://storage.example.test/object/sign/room-duplicate.jpg?token=private-token" });
            context.PropertyAmenities.AddRange(
                new PropertyAmenities { PropertyId = 1, AmenityId = 3 },
                new PropertyAmenities { PropertyId = 2, AmenityId = 4 });
            context.PropertyServiceRates.AddRange(
                new PropertyServiceRates
                {
                    RateId = 1,
                    PropertyId = 1,
                    ServiceId = 11,
                    Price = 120_000m,
                    UnitCode = "room_month",
                    BillingBasis = "fixed_room",
                    EffectiveFrom = new DateOnly(2026, 10, 1),
                    EffectiveTo = new DateOnly(2026, 11, 1)
                },
                new PropertyServiceRates
                {
                    RateId = 2,
                    PropertyId = 1,
                    ServiceId = 11,
                    Price = 150_000m,
                    UnitCode = "room_month",
                    BillingBasis = "fixed_room",
                    EffectiveFrom = new DateOnly(2026, 11, 1)
                },
                new PropertyServiceRates
                {
                    RateId = 3,
                    PropertyId = 2,
                    ServiceId = 11,
                    Price = 180_000m,
                    UnitCode = "room_month",
                    BillingBasis = "fixed_room",
                    EffectiveFrom = new DateOnly(2026, 10, 1)
                });
            context.SaveChanges();
        }

        private static Property Property(int id, string name, string city, string district, string ward) => new()
        {
            PropertyId = id,
            OwnerId = 1,
            PropertyName = name,
            Description = id switch
            {
                1 => "Verified Alpha description",
                2 => "Verified Beta description",
                _ => null
            },
            AddressText = "Sample Road",
            City = city,
            District = district,
            Ward = ward
        };

        private static RoomTypes RoomType(int id, string name, decimal price) => new()
        {
            RoomTypeId = id,
            TypeName = name,
            Price = price
        };

        private static Rooms Room(int id, int propertyId, int roomTypeId, string status) => new()
        {
            RoomId = id,
            PropertyId = propertyId,
            RoomTypeId = roomTypeId,
            RoomCode = $"R{id}",
            AreaSqm = 20m,
            Status = status
        };

        private static Bookings Booking(int id, int roomId, string status, DateTimeOffset expiry) => new()
        {
            BookingId = id,
            RoomId = roomId,
            CustomerId = 2,
            Status = status,
            ExpiryDate = expiry.UtcDateTime
        };

        private static Contracts Contract(int id, int roomId, string status, DateTimeOffset startsAt, DateTimeOffset endsAt) => new()
        {
            ContractId = id,
            RoomId = roomId,
            CustomerId = 2,
            Status = status,
            StartDate = DateOnly.FromDateTime(startsAt.UtcDateTime),
            EndDate = DateOnly.FromDateTime(endsAt.UtcDateTime)
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int ReadCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            ReadCount++;
            return utcNow;
        }
    }

}
