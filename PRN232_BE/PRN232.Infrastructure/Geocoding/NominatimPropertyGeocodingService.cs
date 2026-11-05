using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Infrastructure.Geocoding;

public sealed class NominatimPropertyGeocodingService : IPropertyGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _photonBaseUrl;

    public NominatimPropertyGeocodingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = (configuration["Geocoding:BaseUrl"] ?? "https://nominatim.openstreetmap.org")
            .TrimEnd('/');
        _photonBaseUrl = (configuration["Geocoding:PhotonBaseUrl"] ?? "https://photon.komoot.io")
            .TrimEnd('/');
        var userAgent = configuration["Geocoding:UserAgent"]?.Trim();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            string.IsNullOrWhiteSpace(userAgent) ? "PRN232/1.0" : userAgent);
    }

    public async Task<AddressCoordinates?> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        var requestUri = $"{_baseUrl}/search?format=jsonv2&limit=1&addressdetails=0&accept-language=vi&q={Uri.EscapeDataString(address)}";
        try
        {
            using var document = await GetDocumentAsync(requestUri, cancellationToken);
            var coordinates = ParseCoordinates(document);
            if (coordinates is not null)
                return coordinates;
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            // A provider outage must not prevent saving an otherwise valid address.
        }

        var fallbackUri = $"{_photonBaseUrl}/api/?limit=1&countrycode=VN&q={Uri.EscapeDataString(address)}";
        try
        {
            using var document = await GetDocumentAsync(fallbackUri, cancellationToken);
            return ParsePhotonCoordinates(document);
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            throw new PropertyGeocodingException();
        }
    }

    private async Task<JsonDocument> GetDocumentAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await _httpClient.GetAsync(requestUri, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new PropertyGeocodingException();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
    }

    private static bool IsProviderFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested &&
        exception is HttpRequestException or OperationCanceledException or JsonException or PropertyGeocodingException;

    private static AddressCoordinates? ParsePhotonCoordinates(JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            throw new PropertyGeocodingException();
        }
        if (features.GetArrayLength() == 0)
            return null;

        var result = features[0];
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("geometry", out var geometry) ||
            geometry.ValueKind != JsonValueKind.Object ||
            !geometry.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String || type.GetString() != "Point" ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() != 2 ||
            coordinates[0].ValueKind != JsonValueKind.Number ||
            coordinates[1].ValueKind != JsonValueKind.Number ||
            !coordinates[0].TryGetDecimal(out var longitude) ||
            !coordinates[1].TryGetDecimal(out var latitude))
        {
            throw new PropertyGeocodingException();
        }

        // GeoJSON coordinates are longitude first, latitude second.
        return ValidatedCoordinates(latitude, longitude);
    }

    private static AddressCoordinates? ParseCoordinates(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new PropertyGeocodingException();
        if (document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var result = document.RootElement[0];
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("lat", out var latitudeElement) ||
            !result.TryGetProperty("lon", out var longitudeElement) ||
            latitudeElement.ValueKind != JsonValueKind.String ||
            longitudeElement.ValueKind != JsonValueKind.String ||
            !decimal.TryParse(latitudeElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !decimal.TryParse(longitudeElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            throw new PropertyGeocodingException();
        }

        return ValidatedCoordinates(latitude, longitude);
    }

    private static AddressCoordinates ValidatedCoordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new PropertyGeocodingException();
        return new AddressCoordinates(latitude, longitude);
    }
}
