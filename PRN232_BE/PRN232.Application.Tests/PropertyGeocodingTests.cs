using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Geocoding;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyGeocodingTests
{
    [Fact]
    public async Task Property_create_and_update_persist_fallback_coordinates()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"geocoding-fallback-{Guid.NewGuid()}").Options);
        context.Users.Add(new Users
        {
            UserId = 10, RoleId = 1, FullName = "Admin", Email = "admin@example.test", IsActive = true
        });
        await context.SaveChangesAsync();
        var latitude = 10.779768m;
        var longitude = 106.699042m;
        using var client = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "nominatim.test")
                throw new HttpRequestException("Connection refused");
            return Json(System.Text.Json.JsonSerializer.Serialize(new
            {
                features = new[] { new { geometry = new { type = "Point", coordinates = new[] { longitude, latitude } } } }
            }));
        }));
        var service = new PropertyService(new PropertyRepository(context), CreateService(client));

        var created = await service.CreateAsync(
            new CreatePropertyDto("Test", "1 Công xã Paris", "Bến Nghé", "Quận 1", "Hồ Chí Minh"), 10, true);
        context.ChangeTracker.Clear();
        var persisted = await context.Properties.AsNoTracking().SingleAsync();
        Assert.Equal(latitude, persisted.Latitude);
        Assert.Equal(longitude, persisted.Longitude);

        latitude = 10.776m;
        longitude = 106.7m;
        await service.UpdateAsync(created.PropertyId,
            new UpdatePropertyDto("Test", "Địa chỉ mới", "", "", "Hồ Chí Minh"), 10, true);
        context.ChangeTracker.Clear();
        persisted = await context.Properties.AsNoTracking().SingleAsync();
        Assert.Equal(latitude, persisted.Latitude);
        Assert.Equal(longitude, persisted.Longitude);
    }

    [Fact]
    public async Task Geocode_uses_photon_when_nominatim_connection_fails()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.Host == "nominatim.test")
                throw new HttpRequestException("Connection refused");
            return Json("""{"features":[{"geometry":{"type":"Point","coordinates":[106.6990427,10.7797687]}}]}""");
        }));
        var service = CreateService(client);

        var coordinates = await service.GeocodeAsync("1 Công xã Paris, Hồ Chí Minh");

        Assert.NotNull(coordinates);
        Assert.Equal(10.7797687m, coordinates.Latitude);
        Assert.Equal(106.6990427m, coordinates.Longitude);
        Assert.Equal(2, requests.Count);
        Assert.Equal("photon.test", requests[1].Host);
        Assert.Contains("countrycode=VN", requests[1].Query);
        Assert.Contains(Uri.EscapeDataString("1 Công xã Paris, Hồ Chí Minh"), requests[1].AbsoluteUri);
    }

    [Fact]
    public async Task Geocode_keeps_nominatim_result_without_calling_fallback()
    {
        var count = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            count++;
            return Json("""[{"lat":"10.7797687","lon":"106.6990427"}]""");
        }));

        var coordinates = await CreateService(client).GeocodeAsync("address");

        Assert.Equal(10.7797687m, coordinates!.Latitude);
        Assert.Equal(106.6990427m, coordinates.Longitude);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("[{\"lat\":\"bad\",\"lon\":\"106\"}]")]
    [InlineData("[{\"lat\":\"100\",\"lon\":\"106\"}]")]
    public async Task Geocode_tries_fallback_for_empty_or_invalid_primary_response(string primary)
    {
        using var client = new HttpClient(new StubHandler(request =>
            Json(request.RequestUri!.Host == "nominatim.test"
                ? primary
                : """{"features":[{"geometry":{"type":"Point","coordinates":[106.7,10.8]}}]}""")));

        var coordinates = await CreateService(client).GeocodeAsync("address");

        Assert.Equal(10.8m, coordinates!.Latitude);
        Assert.Equal(106.7m, coordinates.Longitude);
    }

    [Fact]
    public async Task Geocode_returns_null_when_both_providers_find_no_address()
    {
        using var client = new HttpClient(new StubHandler(request =>
            Json(request.RequestUri!.Host == "nominatim.test" ? "[]" : "{\"features\":[]}")));

        Assert.Null(await CreateService(client).GeocodeAsync("unknown address"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"features\":[{\"geometry\":{\"type\":\"Point\",\"coordinates\":[106,100]}}]}")]
    [InlineData("{\"features\":[{\"geometry\":{\"type\":\"Point\",\"coordinates\":[\"106\",10]}}]}")]
    public async Task Geocode_rejects_invalid_fallback_response(string fallback)
    {
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.Host == "nominatim.test"
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(fallback)));

        await Assert.ThrowsAsync<PropertyGeocodingException>(() => CreateService(client).GeocodeAsync("address"));
    }

    [Fact]
    public async Task Geocode_reports_unavailable_when_both_providers_fail()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<PropertyGeocodingException>(() => CreateService(client).GeocodeAsync("address"));
    }

    [Fact]
    public async Task Geocode_preserves_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new HttpClient(new StubHandler(_ => throw new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(client).GeocodeAsync("address", cancellation.Token));
    }

    private static NominatimPropertyGeocodingService CreateService(HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:BaseUrl"] = "https://nominatim.test",
            ["Geocoding:PhotonBaseUrl"] = "https://photon.test"
        }).Build());

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
