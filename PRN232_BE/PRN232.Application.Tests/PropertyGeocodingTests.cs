using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Infrastructure.Geocoding;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyGeocodingTests
{
    [Fact]
    public async Task Search_uses_country_restriction_address_components_and_returns_multiple_candidates()
    {
        var requests = new List<HttpRequestMessage>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request);
            return Json("""
                [
                  {
                    "lat":"10.8","lon":"106.7","display_name":"Ward centre",
                    "addresstype":"suburb",
                    "address":{"suburb":"Bến Thành","state":"Ho Chi Minh City","country_code":"vn"}
                  },
                  {
                    "lat":"10.779","lon":"106.699","display_name":"12 Lê Lợi",
                    "addresstype":"house",
                    "address":{"house_number":"12","road":"Lê Lợi","suburb":"Bến Thành","city":"Ho Chi Minh City","country_code":"vn"}
                  }
                ]
                """);
        }));
        var service = CreateService(client);

        var query = CreateQuery("12 Lê Lợi, Phường Bến Thành, Thành phố Hồ Chí Minh, Việt Nam");
        var candidates = await service.FindNominatimCandidatesAsync(query);
        var cachedCandidates = await service.FindNominatimCandidatesAsync(query);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("commune", candidates[0].Precision);
        Assert.Equal("house", candidates[1].Precision);
        Assert.Equal("12", candidates[1].HouseNumber);
        Assert.Null(candidates[1].WardName);
        Assert.Contains(candidates[1].AdministrativeAreas!, area =>
            area.Source == "suburb" && area.Name == "Bến Thành");
        Assert.Equal("Ho Chi Minh City", candidates[1].ProvinceName);
        Assert.Equal(candidates, cachedCandidates);
        var firstRequest = Assert.Single(requests);
        var requestUri = firstRequest.RequestUri!;
        Assert.Contains("countrycodes=vn", requestUri.Query);
        Assert.Contains("addressdetails=1", requestUri.Query);
        Assert.Contains("limit=5", requestUri.Query);
        Assert.Contains("PRN232", string.Join(" ", firstRequest.Headers.UserAgent));
    }

    [Theory]
    [InlineData("primary")]
    [InlineData("secondary")]
    public async Task Search_classifies_nominatim_road_classes_as_street(string roadClass)
    {
        using var client = new HttpClient(new StubHandler(_ => Json("""
            [
              {
                "lat":"10.7666","lon":"106.6961","display_name":"Đường Nguyễn Thái Học",
                "addresstype":"ROAD_CLASS","category":"highway","type":"ROAD_CLASS",
                "address":{"road":"Đường Nguyễn Thái Học","suburb":"Phường Cầu Ông Lãnh",
                  "state":"Thành phố Hồ Chí Minh","country_code":"vn"}
              }
            ]
            """.Replace("ROAD_CLASS", roadClass, StringComparison.Ordinal))));

        var candidates = await CreateService(client).FindNominatimCandidatesAsync(new PropertyGeocodingQuery(
            "65D, Nguyễn Thái Học, Phường Cầu Ông Lãnh, Thành phố Hồ Chí Minh",
            "Nguyễn Thái Học",
            "65D",
            "Phường Cầu Ông Lãnh",
            "Thành phố Hồ Chí Minh"));

        Assert.Equal("street", Assert.Single(candidates).Precision);
    }

    [Fact]
    public async Task Photon_preserves_all_administrative_area_labels_for_ward_matching()
    {
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.Host == "nominatim.test"
                ? Json("[]")
                : Json("""
                    {"features":[{
                      "geometry":{"type":"Point","coordinates":[106.6962,10.7666]},
                      "properties":{"name":"Nguyễn Thái Học","type":"street","street":"Nguyễn Thái Học",
                        "osm_key":"highway","osm_value":"primary","suburb":"Khu phố 16",
                        "district":"Cầu Ông Lãnh","city":"Ho Chi Minh City","countrycode":"vn"}
                    }]}
                    """)));

        var candidates = await CreateService(client).FindPhotonCandidatesAsync(new PropertyGeocodingQuery(
            "65D, Nguyễn Thái Học, Phường Cầu Ông Lãnh, Thành phố Hồ Chí Minh",
            "Nguyễn Thái Học",
            "65D",
            "Phường Cầu Ông Lãnh",
            "Thành phố Hồ Chí Minh"));

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.WardName);
        Assert.Contains(candidate.AdministrativeAreas!, area =>
            area.Source == "district" && area.Name == "Cầu Ông Lãnh");
        Assert.Contains(candidate.AdministrativeAreas!, area =>
            area.Source == "suburb" && area.Name == "Khu phố 16");
        Assert.Equal("Nguyễn Thái Học", candidate.StreetName);
        Assert.Equal("street", candidate.Precision);
    }

    [Fact]
    public async Task Photon_does_not_classify_a_bus_stop_as_a_building_or_house()
    {
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.Host == "nominatim.test"
                ? Json("[]")
                : Json("""
                    {"features":[{
                      "geometry":{"type":"Point","coordinates":[106.6962,10.7666]},
                      "properties":{"name":"Cầu Ông Lãnh","type":"house","housenumber":"65D",
                        "street":"Nguyễn Thái Học","osm_key":"highway","osm_value":"bus_stop",
                        "district":"Cầu Ông Lãnh","state":"Ho Chi Minh City","countrycode":"vn"}
                    }]}
                    """)));

        var candidates = await CreateService(client).FindPhotonCandidatesAsync(new PropertyGeocodingQuery(
            "65D, Nguyễn Thái Học, Phường Cầu Ông Lãnh, Thành phố Hồ Chí Minh",
            "Nguyễn Thái Học",
            "65D",
            "Phường Cầu Ông Lãnh",
            "Thành phố Hồ Chí Minh"));

        Assert.Equal("poi", Assert.Single(candidates).Precision);
    }

    [Fact]
    public async Task Search_photon_uses_structured_address_components_and_maps_coordinates()
    {
        var requests = new List<Uri>();
        var photonAcceptLanguages = new List<string>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.Host == "nominatim.test")
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            photonAcceptLanguages.Add(string.Join(",", request.Headers.AcceptLanguage.Select(item => item.Value)));

            return Json("""
                {"features":[{
                  "geometry":{"type":"Point","coordinates":[106.6990427,10.7797687]},
                  "properties":{"name":"12 Lê Lợi","type":"house","housenumber":"12",
                    "street":"Lê Lợi","locality":"Bến Thành","state":"Ho Chi Minh City","countrycode":"vn"}
                }]}
                """);
        }));
        var service = CreateService(client);
        var query = CreateQuery(
            "12 Lê Lợi, Thành phố Hồ Chí Minh",
            "Lê Lợi",
            "12",
            "Bến Thành",
            "Thành phố Hồ Chí Minh");

        var candidates = await service.FindPhotonCandidatesAsync(query);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Photon", candidate.Provider);
        Assert.Equal(10.7797687m, candidate.Coordinates.Latitude);
        Assert.Equal(106.6990427m, candidate.Coordinates.Longitude);
        Assert.Equal("house", candidate.Precision);
        Assert.Equal("vn", candidate.CountryCode);
        Assert.Single(requests);
        Assert.EndsWith("/structured", requests[0].AbsolutePath);
        Assert.Contains("countrycode=VN", requests[0].Query);
        Assert.Contains("housenumber=12", requests[0].Query);
        Assert.Contains("street=L%C3%AA%20L%E1%BB%A3i", requests[0].Query);
        Assert.DoesNotContain("lang=", requests[0].Query);
        Assert.Equal("vi", Assert.Single(photonAcceptLanguages));
    }

    [Fact]
    public async Task Search_returns_empty_candidates_when_provider_returns_empty_list()
    {
        using var client = new HttpClient(new StubHandler(request => Json(
            request.RequestUri!.Host == "nominatim.test" ? "[]" : "{\"features\":[]}")));

        var candidates = await CreateService(client).FindNominatimCandidatesAsync(
            CreateQuery("No matching address"));

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Search_throws_safely_when_provider_returns_server_error()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<PropertyGeocodingException>(() =>
            CreateService(client).FindNominatimCandidatesAsync(CreateQuery("address")));
    }

    [Fact]
    public async Task Search_does_not_cache_provider_failures()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json("""
                    [{
                      "lat":"10.7666","lon":"106.6961","display_name":"Đường Nguyễn Thái Học",
                      "addresstype":"primary","category":"highway","type":"primary",
                      "address":{"road":"Nguyễn Thái Học","suburb":"Phường Cầu Ông Lãnh",
                        "state":"Thành phố Hồ Chí Minh","country_code":"vn"}
                    }]
                    """);
        }));
        var service = CreateService(client);
        var query = CreateQuery("65D, Nguyễn Thái Học, Thành phố Hồ Chí Minh");

        await Assert.ThrowsAsync<PropertyGeocodingException>(() =>
            service.FindNominatimCandidatesAsync(query));
        var candidates = await service.FindNominatimCandidatesAsync(query);

        Assert.Equal(10.7666m, Assert.Single(candidates).Coordinates.Latitude);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task Search_reports_rate_limit_separately_from_other_provider_failures()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        await Assert.ThrowsAsync<PropertyGeocodingRateLimitedException>(() =>
            CreateService(client).FindNominatimCandidatesAsync(CreateQuery("address")));
    }

    [Fact]
    public async Task Search_maps_provider_timeout_to_safe_unavailable_error()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new TaskCanceledException()));

        await Assert.ThrowsAsync<PropertyGeocodingException>(() =>
            CreateService(client).FindNominatimCandidatesAsync(CreateQuery("address")));
    }

    [Fact]
    public async Task Search_caches_empty_provider_result_for_repeated_query()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            requestCount++;
            return Json("[]");
        }));
        var service = CreateService(client);
        var query = CreateQuery("No matching address");

        Assert.Empty(await service.FindNominatimCandidatesAsync(query));
        Assert.Empty(await service.FindNominatimCandidatesAsync(query));

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task Search_preserves_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new HttpClient(new StubHandler(_ => throw new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(client).FindNominatimCandidatesAsync(
                CreateQuery("address"),
                cancellation.Token));
    }

    private static NominatimPropertyGeocodingService CreateService(HttpClient client) => new(
        client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:BaseUrl"] = "https://nominatim.test",
            ["Geocoding:PhotonBaseUrl"] = "https://photon.test",
            ["Geocoding:UserAgent"] = "PRN232.Tests/1.0 (+https://github.com/Chau165/PRN232)"
        }).Build(),
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System);

    private static PropertyGeocodingQuery CreateQuery(
        string fullAddress,
        string streetName = "Lê Lợi",
        string? houseNumber = "12",
        string wardName = "Phường Bến Thành",
        string provinceName = "Thành phố Hồ Chí Minh") => new(
            fullAddress,
            streetName,
            houseNumber,
            wardName,
            provinceName);

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
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
