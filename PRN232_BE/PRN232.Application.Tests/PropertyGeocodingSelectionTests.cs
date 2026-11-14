using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.AdministrativeDivisions;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Geocoding;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyGeocodingSelectionTests
{
    [Fact]
    public async Task Create_uses_matching_street_level_candidate_for_65D_and_rejects_wrong_house_result()
    {
        await using var context = await CreateContextAsync();
        var requests = new List<Uri>();
        using var client = new HttpClient(new FixtureHandler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                return Json("""
                    [
                      {
                        "lat":"10.7667","lon":"106.6960","display_name":"227 Nguyễn Văn Cừ",
                        "addresstype":"house","category":"place","type":"house",
                        "address":{"house_number":"227","road":"Nguyễn Văn Cừ",
                          "suburb":"Khu phố 16","district":"Cầu Ông Lãnh",
                          "state":"Thành phố Hồ Chí Minh","country_code":"vn"}
                      },
                      {
                        "lat":"10.7666095","lon":"106.6961680","display_name":"Đường Nguyễn Thái Học",
                        "addresstype":"primary","category":"highway","type":"primary",
                        "address":{"road":"Đường Nguyễn Thái Học","suburb":"Khu phố 16",
                          "district":"Cầu Ông Lãnh","state":"Thành phố Hồ Chí Minh","country_code":"vn"}
                      },
                      {
                        "lat":"10.7657545","lon":"106.6966857","display_name":"Đường Nguyễn Thái Học",
                        "addresstype":"secondary","category":"highway","type":"secondary",
                        "address":{"road":"Đường Nguyễn Thái Học","suburb":"Khu phố 17",
                          "district":"Cầu Ông Lãnh","state":"Ho Chi Minh City","country_code":"vn"}
                      }
                    ]
                    """);
            }

            return Json("{\"features\":[]}");
        }));
        var geocoder = CreateGeocoder(client);
        var service = CreatePropertyService(context, geocoder);

        var created = await service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true);

        Assert.Equal(10.7666095m, created.Latitude);
        Assert.Equal(106.6961680m, created.Longitude);
        Assert.Single(requests);
        Assert.Contains("/search?", requests[0].PathAndQuery);
        Assert.DoesNotContain("/structured", requests[0].AbsoluteUri);
        var persisted = await context.Properties.SingleAsync();
        Assert.Equal(created.Latitude, persisted.Latitude);
        Assert.Equal(created.Longitude, persisted.Longitude);
    }

    [Fact]
    public async Task Create_falls_back_to_structured_photon_when_nominatim_candidate_has_wrong_street()
    {
        await using var context = await CreateContextAsync();
        var requests = new List<Uri>();
        using var client = new HttpClient(new FixtureHandler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                return Json("""
                    [{
                      "lat":"10.7667","lon":"106.6960","display_name":"227 Nguyễn Văn Cừ",
                      "addresstype":"house","category":"place","type":"house",
                      "address":{"house_number":"227","road":"Nguyễn Văn Cừ",
                        "suburb":"Khu phố 16","district":"Cầu Ông Lãnh",
                        "state":"Thành phố Hồ Chí Minh","country_code":"vn"}
                    }]
                    """);
            }

            return Json("""
                {"features":[{
                  "geometry":{"type":"Point","coordinates":[106.6962,10.7666]},
                  "properties":{"name":"Nguyễn Thái Học","type":"street","street":"Đường Nguyễn Thái Học",
                    "osm_key":"highway","osm_value":"primary","suburb":"Khu phố 16",
                    "district":"Cầu Ông Lãnh","state":"Ho Chi Minh City","countrycode":"vn"}
                }]}
                """);
        }));
        var service = CreatePropertyService(context, CreateGeocoder(client));

        var created = await service.CreateAsync(
            new CreatePropertyDto("BVO", "65D Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true);

        Assert.Equal(10.7666m, created.Latitude);
        Assert.Equal(106.6962m, created.Longitude);
        Assert.Equal(2, requests.Count);
        Assert.EndsWith("/search", requests[0].AbsolutePath);
        Assert.EndsWith("/structured", requests[1].AbsolutePath);
        Assert.Contains("housenumber=65D", requests[1].Query);
        Assert.Contains("street=Nguy%E1%BB%85n%20Th%C3%A1i%20H%E1%BB%8Dc", requests[1].Query);
    }

    [Fact]
    public async Task Create_rejects_candidate_from_a_different_street_without_creating_property()
    {
        await using var context = await CreateContextAsync();
        var geocoder = new FakeGeocodingService(
            [CreateCandidate(streetName: "Nguyễn Văn Cừ")],
            Array.Empty<AddressGeocodingCandidate>());
        var service = CreatePropertyService(context, geocoder);

        var exception = await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Contains("không khớp đủ tin cậy", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Properties);
        Assert.Equal(1, geocoder.NominatimCalls);
        Assert.Equal(1, geocoder.PhotonCalls);
    }

    [Fact]
    public async Task Create_rejects_candidate_with_a_different_house_number()
    {
        await using var context = await CreateContextAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [CreateCandidate(houseNumber: "66")],
            Array.Empty<AddressGeocodingCandidate>()));

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Create_rejects_house_precision_candidate_without_a_verified_house_number()
    {
        await using var context = await CreateContextAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [CreateCandidate(precision: "house", houseNumber: null)],
            Array.Empty<AddressGeocodingCandidate>()));

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Create_rejects_explicit_ward_mismatch_even_when_an_alternative_area_name_matches()
    {
        await using var context = await CreateContextAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [CreateCandidate(wardName: "Phường Bến Thành")],
            Array.Empty<AddressGeocodingCandidate>()));

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Create_rejects_spatially_ambiguous_candidates_of_equal_quality()
    {
        await using var context = await CreateContextAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [
                CreateCandidate(precision: "street", houseNumber: null, providerRank: 0),
                CreateCandidate(
                    coordinates: new AddressCoordinates(10.80m, 106.72m),
                    precision: "street",
                    houseNumber: null,
                    providerRank: 1)
            ],
            Array.Empty<AddressGeocodingCandidate>()));

        var exception = await Assert.ThrowsAsync<PropertyValidationException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Contains("nhiều vị trí", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Create_uses_ambiguous_ward_metadata_when_explicit_ward_name_is_missing()
    {
        await using var context = await CreateContextAsync();
        var candidate = CreateCandidate(
            precision: "street",
            houseNumber: null,
            wardName: null,
            administrativeAreas:
            [
                new AddressAdministrativeArea("suburb", "Khu phố 16"),
                new AddressAdministrativeArea("district", "Cầu Ông Lãnh")
            ]);
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [candidate],
            Array.Empty<AddressGeocodingCandidate>()));

        var created = await service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true);

        Assert.Equal(10.7666m, created.Latitude);
        Assert.Equal(106.6962m, created.Longitude);
    }

    [Fact]
    public async Task Update_does_not_persist_new_address_or_coordinates_when_geocoding_fails()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 21,
            OwnerId = 10,
            PropertyName = "BVO",
            AddressText = "65D, Nguyễn Thái Học",
            Ward = "Phường Cầu Ông Lãnh",
            District = string.Empty,
            City = "Thành phố Hồ Chí Minh",
            Latitude = 10.7666m,
            Longitude = 106.6962m
        });
        await context.SaveChangesAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            [CreateCandidate(houseNumber: "65D")],
            Array.Empty<AddressGeocodingCandidate>()));

        await Assert.ThrowsAsync<PropertyValidationException>(() => service.UpdateAsync(
            21,
            new UpdatePropertyDto(
                "BVO mới",
                "66A, Nguyễn Thái Học",
                "Phường Cầu Ông Lãnh",
                string.Empty,
                "Thành phố Hồ Chí Minh",
                null,
                "79",
                "26758"),
            requestingUserId: 10,
            isAdmin: true));

        var unchanged = await context.Properties.AsNoTracking().SingleAsync(property => property.PropertyId == 21);
        Assert.Equal("65D, Nguyễn Thái Học", unchanged.AddressText);
        Assert.Equal(10.7666m, unchanged.Latitude);
        Assert.Equal(106.6962m, unchanged.Longitude);
    }

    [Fact]
    public async Task Update_does_not_geocode_or_change_coordinates_when_address_is_unchanged()
    {
        await using var context = await CreateContextAsync();
        context.Properties.Add(new Property
        {
            PropertyId = 21,
            OwnerId = 10,
            PropertyName = "BVO",
            AddressText = "65D, Nguyễn Thái Học",
            Ward = "Phường Cầu Ông Lãnh",
            District = string.Empty,
            City = "Thành phố Hồ Chí Minh",
            Latitude = 10.7666m,
            Longitude = 106.6962m
        });
        await context.SaveChangesAsync();
        var geocoder = new FakeGeocodingService(
            Array.Empty<AddressGeocodingCandidate>(),
            Array.Empty<AddressGeocodingCandidate>());
        var service = CreatePropertyService(context, geocoder);

        var updated = await service.UpdateAsync(
            21,
            new UpdatePropertyDto(
                "BVO đổi tên",
                "65D, Nguyễn Thái Học",
                "Phường Cầu Ông Lãnh",
                string.Empty,
                "Thành phố Hồ Chí Minh",
                null,
                "79",
                "26758"),
            requestingUserId: 10,
            isAdmin: true);

        Assert.Equal("BVO đổi tên", updated!.PropertyName);
        Assert.Equal(0, geocoder.NominatimCalls);
        Assert.Equal(0, geocoder.PhotonCalls);
        Assert.Equal(10.7666m, updated.Latitude);
        Assert.Equal(106.6962m, updated.Longitude);
    }

    [Fact]
    public async Task Create_preserves_caller_cancellation_without_creating_property()
    {
        await using var context = await CreateContextAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            Array.Empty<AddressGeocodingCandidate>(),
            Array.Empty<AddressGeocodingCandidate>(),
            nominatimFailure: new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true,
            cancellationToken: cancellation.Token));

        Assert.Empty(context.Properties);
    }

    [Fact]
    public async Task Create_reports_rate_limit_when_no_provider_can_return_a_trusted_candidate()
    {
        await using var context = await CreateContextAsync();
        var service = CreatePropertyService(context, new FakeGeocodingService(
            Array.Empty<AddressGeocodingCandidate>(),
            Array.Empty<AddressGeocodingCandidate>(),
            nominatimFailure: new PropertyGeocodingRateLimitedException(),
            photonFailure: new PropertyGeocodingException()));

        var exception = await Assert.ThrowsAsync<PropertyGeocodingRateLimitedException>(() => service.CreateAsync(
            new CreatePropertyDto("BVO", "65D, Nguyễn Thái Học", "79", "26758"),
            ownerId: 10,
            isAdmin: true));

        Assert.Contains("giới hạn", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Properties);
    }

    private static AddressGeocodingCandidate CreateCandidate(
        AddressCoordinates? coordinates = null,
        string precision = "house",
        string? houseNumber = "65D",
        string streetName = "Đường Nguyễn Thái Học",
        string? wardName = "Phường Cầu Ông Lãnh",
        int providerRank = 0,
        IReadOnlyList<AddressAdministrativeArea>? administrativeAreas = null) => new(
            coordinates ?? new AddressCoordinates(10.7666m, 106.6962m),
            "Đường Nguyễn Thái Học, Cầu Ông Lãnh, Thành phố Hồ Chí Minh",
            "Nominatim",
            precision,
            houseNumber,
            streetName,
            wardName,
            "Ho Chi Minh City",
            "vn",
            administrativeAreas ??
            [
                new AddressAdministrativeArea("district", "Cầu Ông Lãnh"),
                new AddressAdministrativeArea("suburb", "Khu phố 16")
            ],
            [new AddressAdministrativeArea("state", "Ho Chi Minh City")],
            precision == "street" ? "highway" : "place",
            precision == "street" ? "primary" : "house",
            providerRank);

    private static PropertyService CreatePropertyService(
        AppDbContext context,
        IPropertyGeocodingService geocoder) => new(
            new PropertyRepository(context),
            new AdministrativeDivisionCatalog(),
            geocoder);

    private static NominatimPropertyGeocodingService CreateGeocoder(HttpClient client) => new(
        client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:BaseUrl"] = "https://nominatim.test",
            ["Geocoding:PhotonBaseUrl"] = "https://photon.test",
            ["Geocoding:UserAgent"] = "PRN232.Tests/1.0 (+https://github.com/Chau165/PRN232)"
        }).Build(),
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System);

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"property-geocoding-{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        context.Users.Add(new Users
        {
            UserId = 10,
            RoleId = 1,
            FullName = "Admin",
            Email = "admin@example.test",
            IsActive = true
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FakeGeocodingService(
        IReadOnlyList<AddressGeocodingCandidate> nominatimCandidates,
        IReadOnlyList<AddressGeocodingCandidate> photonCandidates,
        Exception? nominatimFailure = null,
        Exception? photonFailure = null) : IPropertyGeocodingService
    {
        public int NominatimCalls { get; private set; }

        public int PhotonCalls { get; private set; }

        public PropertyGeocodingQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindNominatimCandidatesAsync(
            PropertyGeocodingQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NominatimCalls++;
            LastQuery = query;
            return nominatimFailure is null
                ? Task.FromResult(nominatimCandidates)
                : Task.FromException<IReadOnlyList<AddressGeocodingCandidate>>(nominatimFailure);
        }

        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindPhotonCandidatesAsync(
            PropertyGeocodingQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PhotonCalls++;
            LastQuery = query;
            return photonFailure is null
                ? Task.FromResult(photonCandidates)
                : Task.FromException<IReadOnlyList<AddressGeocodingCandidate>>(photonFailure);
        }
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
