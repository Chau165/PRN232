using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Normalizers;
using PRN232.Application.Services;

namespace PRN232.Infrastructure.Geocoding;

public sealed class NominatimPropertyGeocodingService : IPropertyGeocodingService
{
    // This gate enforces the public Nominatim limit within one API process.
    // Multi-instance deployments must route through one shared rate-limited proxy.
    private static readonly SemaphoreSlim NominatimRateLimit = new(1, 1);
    private static DateTimeOffset NextNominatimRequestAt = DateTimeOffset.MinValue;
    private static readonly HashSet<string> RoadTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "motorway", "trunk", "primary", "secondary", "tertiary", "unclassified",
        "residential", "living_street", "service", "pedestrian", "road",
        "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link",
        "footway", "cycleway", "path", "track", "steps"
    };
    private static readonly HashSet<string> PoiObjectKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "amenity", "shop", "tourism", "leisure", "office", "public_transport", "railway",
        "aeroway", "historic", "craft", "healthcare", "emergency", "sport", "club"
    };
    private const string CacheVersion = "v2";
    private static readonly TimeSpan CandidateCacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan EmptyCandidateCacheDuration = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NominatimPropertyGeocodingService> _logger;
    private readonly string _baseUrl;
    private readonly string _photonBaseUrl;

    public NominatimPropertyGeocodingService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<NominatimPropertyGeocodingService>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<NominatimPropertyGeocodingService>.Instance;
        _baseUrl = (configuration["Geocoding:BaseUrl"] ?? "https://nominatim.openstreetmap.org")
            .TrimEnd('/');
        _photonBaseUrl = (configuration["Geocoding:PhotonBaseUrl"] ?? "https://photon.komoot.io")
            .TrimEnd('/');
        var userAgent = configuration["Geocoding:UserAgent"]?.Trim();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            string.IsNullOrWhiteSpace(userAgent)
                ? "PRN232/1.0 (+https://github.com/Chau165/PRN232)"
                : userAgent);
    }

    public Task<IReadOnlyList<AddressGeocodingCandidate>> FindNominatimCandidatesAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken = default)
    {
        return GetCachedCandidatesAsync(
            "nominatim",
            query.FullAddress,
            cancellationToken,
            token => SearchNominatimAsync(query, token));
    }

    public Task<IReadOnlyList<AddressGeocodingCandidate>> FindPhotonCandidatesAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken = default)
    {
        return GetCachedCandidatesAsync(
            "photon",
            query.FullAddress,
            cancellationToken,
            token => SearchPhotonAsync(query, token));
    }

    private async Task<IReadOnlyList<AddressGeocodingCandidate>> GetCachedCandidatesAsync(
        string provider,
        string address,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<IReadOnlyList<AddressGeocodingCandidate>>> searchAsync)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Array.Empty<AddressGeocodingCandidate>();
        }

        var cacheKey = CreateCacheKey(provider, address);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<AddressGeocodingCandidate>? cachedCandidates) &&
            cachedCandidates is not null)
        {
            return cachedCandidates;
        }

        try
        {
            var candidates = await searchAsync(cancellationToken);
            _logger.LogInformation(
                "Geocoding provider returned candidates. TraceId={TraceId} Provider={Provider} CandidateCount={CandidateCount}",
                Activity.Current?.TraceId.ToString(),
                provider,
                candidates.Count);
            return CacheCandidates(cacheKey, candidates);
        }
        catch (PropertyGeocodingException) when (!cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Geocoding provider timed out. TraceId={TraceId} Provider={Provider}",
                Activity.Current?.TraceId.ToString(),
                provider);
            throw new PropertyGeocodingException();
        }
        catch (Exception exception) when (
            !cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Geocoding provider request failed. TraceId={TraceId} Provider={Provider} FailureType={FailureType}",
                Activity.Current?.TraceId.ToString(),
                provider,
                exception.GetType().Name);
            throw new PropertyGeocodingException();
        }
    }

    private async Task<IReadOnlyList<AddressGeocodingCandidate>> SearchNominatimAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken)
    {
        await WaitForNominatimRateLimitAsync(cancellationToken);
        var requestUri = $"{_baseUrl}/search?format=jsonv2&limit=5&addressdetails=1&countrycodes=vn&accept-language=vi&q={Uri.EscapeDataString(query.FullAddress)}";
        using var document = await GetDocumentAsync(requestUri, "Nominatim", cancellationToken);
        return ParseNominatimCandidates(document);
    }

    private async Task<IReadOnlyList<AddressGeocodingCandidate>> SearchPhotonAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildPhotonStructuredSearchUri(query);
        using var document = await GetDocumentAsync(requestUri, "Photon", cancellationToken, "vi");
        return ParsePhotonCandidates(document);
    }

    private string BuildPhotonStructuredSearchUri(PropertyGeocodingQuery query)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("limit", "5"),
            new("countrycode", "VN"),
            new("state", query.ProvinceName),
            new("district", query.WardName),
            new("street", query.StreetName)
        };
        if (!string.IsNullOrWhiteSpace(query.HouseNumber))
        {
            parameters.Add(new KeyValuePair<string, string>("housenumber", query.HouseNumber));
        }

        var queryString = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        return $"{_photonBaseUrl}/structured?{queryString}";
    }

    private async Task WaitForNominatimRateLimitAsync(CancellationToken cancellationToken)
    {
        if (!UsesPublicNominatimEndpoint())
        {
            return;
        }

        await NominatimRateLimit.WaitAsync(cancellationToken);
        try
        {
            var delay = NextNominatimRequestAt - _timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _timeProvider, cancellationToken);
            }

            NextNominatimRequestAt = _timeProvider.GetUtcNow().AddSeconds(1);
        }
        finally
        {
            NominatimRateLimit.Release();
        }
    }

    private bool UsesPublicNominatimEndpoint()
    {
        return Uri.TryCreate(_baseUrl, UriKind.Absolute, out var baseUri) &&
            string.Equals(
                baseUri.Host,
                "nominatim.openstreetmap.org",
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonDocument> GetDocumentAsync(
        string requestUri,
        string provider,
        CancellationToken cancellationToken,
        string? acceptLanguage = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(acceptLanguage))
        {
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        }
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Geocoding provider returned a failure status. TraceId={TraceId} Provider={Provider} StatusCode={StatusCode}",
                Activity.Current?.TraceId.ToString(),
                provider,
                (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new PropertyGeocodingRateLimitedException();
            }

            throw new PropertyGeocodingException();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
    }

    private static IReadOnlyList<AddressGeocodingCandidate> ParseNominatimCandidates(
        JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new PropertyGeocodingException();
        }

        var candidates = new List<AddressGeocodingCandidate>();
        foreach (var result in root.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object ||
                !TryReadCoordinateString(result, "lat", out var latitude) ||
                !TryReadCoordinateString(result, "lon", out var longitude))
            {
                continue;
            }

            var address = TryReadObject(result, "address");
            var houseNumber = ReadString(address, "house_number", "house_name");
            var addressType = ReadString(result, "addresstype");
            var osmKey = ReadString(result, "category", "class");
            var osmValue = ReadString(result, "type") ?? addressType;
            var displayAddress = ReadString(result, "display_name") ?? string.Empty;
            var precision = ClassifyPrecision(addressType, osmKey, osmValue, houseNumber);
            candidates.Add(new AddressGeocodingCandidate(
                new AddressCoordinates(latitude, longitude),
                displayAddress,
                "Nominatim",
                precision,
                houseNumber,
                ReadString(address, "road", "pedestrian", "street"),
                ReadString(address, "ward"),
                ReadProvinceName(address, "state", "province", "region", "city", "county"),
                ReadString(address, "country_code"),
                ReadAdministrativeAreas(
                    address,
                    "ward", "suburb", "city_district", "borough", "quarter", "neighbourhood",
                    "district", "village", "town", "municipality"),
                ReadAdministrativeAreas(address, "state", "province", "region", "city", "county"),
                osmKey,
                osmValue,
                candidates.Count));
        }

        return candidates;
    }

    private static IReadOnlyList<AddressGeocodingCandidate> ParsePhotonCandidates(
        JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            throw new PropertyGeocodingException();
        }

        var candidates = new List<AddressGeocodingCandidate>();
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.Object ||
                !feature.TryGetProperty("geometry", out var geometry) ||
                geometry.ValueKind != JsonValueKind.Object ||
                !geometry.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String || type.GetString() != "Point" ||
                !geometry.TryGetProperty("coordinates", out var coordinates) ||
                coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() != 2 ||
                coordinates[0].ValueKind != JsonValueKind.Number ||
                coordinates[1].ValueKind != JsonValueKind.Number ||
                !coordinates[0].TryGetDecimal(out var longitude) ||
                !coordinates[1].TryGetDecimal(out var latitude) ||
                !IsValidCoordinatePair(latitude, longitude))
            {
                continue;
            }

            var properties = TryReadObject(feature, "properties");
            var houseNumber = ReadString(properties, "housenumber", "house_number");
            var photonLayer = ReadString(properties, "type");
            var osmKey = ReadString(properties, "osm_key");
            var osmValue = ReadString(properties, "osm_value");
            var displayAddress = ReadString(properties, "name", "street") ?? string.Empty;
            candidates.Add(new AddressGeocodingCandidate(
                new AddressCoordinates(latitude, longitude),
                displayAddress,
                "Photon",
                ClassifyPrecision(photonLayer, osmKey, osmValue, houseNumber),
                houseNumber,
                ReadPhotonStreetName(properties, osmKey, osmValue),
                ReadString(properties, "ward"),
                ReadProvinceName(properties, "state", "province", "region", "city", "county"),
                ReadPhotonCountryCode(properties),
                ReadAdministrativeAreas(
                    properties,
                    "ward", "city_district", "district", "suburb", "locality", "neighbourhood",
                    "borough", "quarter", "village", "town", "municipality"),
                ReadAdministrativeAreas(properties, "state", "province", "region", "city", "county"),
                osmKey,
                osmValue,
                candidates.Count));
        }

        return candidates;
    }

    private static string? ReadPhotonCountryCode(JsonElement properties)
    {
        var code = ReadString(properties, "countrycode", "country_code");
        if (code is not null)
        {
            return code;
        }

        var normalizedCountryName = PublicAreaNameNormalizer.Normalize(ReadString(properties, "country"));
        return normalizedCountryName is "vietnam" or "viet nam" ? "vn" : null;
    }

    private static string? ReadPhotonStreetName(JsonElement properties, string? osmKey, string? osmValue)
    {
        var streetName = ReadString(properties, "street");
        if (streetName is not null)
        {
            return streetName;
        }

        return IsRoadTag(osmKey, osmValue)
            ? ReadString(properties, "name")
            : null;
    }

    private static string ClassifyPrecision(
        string? featureType,
        string? osmKey,
        string? osmValue,
        string? houseNumber)
    {
        var normalizedKey = osmKey?.Trim();
        var normalizedValue = osmValue?.Trim();
        var normalizedType = featureType?.Trim();

        if (string.Equals(normalizedKey, "highway", StringComparison.OrdinalIgnoreCase))
        {
            return RoadTypes.Contains(normalizedValue ?? normalizedType ?? string.Empty)
                ? "street"
                : "poi";
        }

        if (normalizedKey is not null && PoiObjectKeys.Contains(normalizedKey))
        {
            return "poi";
        }

        if (string.Equals(normalizedKey, "building", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(normalizedValue, "no", StringComparison.OrdinalIgnoreCase))
            {
                return "unknown";
            }

            return string.IsNullOrWhiteSpace(houseNumber) ? "building" : "house";
        }

        if (string.Equals(normalizedKey, "place", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedValue, "house", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(houseNumber) ? "building" : "house";
        }

        if (IsRoadType(normalizedType) || IsRoadType(normalizedValue))
        {
            return "street";
        }

        var hasHouseNumber = !string.IsNullOrWhiteSpace(houseNumber);
        if (string.Equals(normalizedType, "house", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedKey is null)
            {
                return hasHouseNumber ? "house" : "building";
            }

            return "unknown";
        }

        if (string.Equals(normalizedType, "building", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedKey is null ||
                string.Equals(normalizedKey, "building", StringComparison.OrdinalIgnoreCase))
            {
                return hasHouseNumber ? "house" : "building";
            }

            return "unknown";
        }

        return normalizedType?.ToLowerInvariant() switch
        {
            "road" or "street" or "highway" => "street",
            "ward" or "suburb" or "city_district" or "locality" or "district" => "commune",
            "city" or "state" or "province" => "province",
            _ => "unknown"
        };
    }

    private static bool IsRoadTag(string? osmKey, string? osmValue)
    {
        return string.Equals(osmKey, "highway", StringComparison.OrdinalIgnoreCase) &&
            IsRoadType(osmValue);
    }

    private static bool IsRoadType(string? value)
    {
        return value is not null && RoadTypes.Contains(value.Trim());
    }

    private static string? ReadProvinceName(JsonElement properties, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(properties, propertyName);
            if (value is not null && PublicAreaNameNormalizer.Normalize(value) != "vietnam")
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<AddressAdministrativeArea> ReadAdministrativeAreas(
        JsonElement properties,
        params string[] propertyNames)
    {
        var administrativeAreas = new List<AddressAdministrativeArea>();
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(properties, propertyName);
            if (value is not null)
            {
                administrativeAreas.Add(new AddressAdministrativeArea(propertyName, value));
            }
        }

        return administrativeAreas;
    }

    private static bool TryReadCoordinateString(
        JsonElement element,
        string propertyName,
        out decimal value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var coordinate) &&
            coordinate.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                coordinate.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            value is >= -180m and <= 180m;
    }

    private static JsonElement TryReadObject(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
        }

        return null;
    }

    private IReadOnlyList<AddressGeocodingCandidate> CacheCandidates(
        string key,
        IReadOnlyList<AddressGeocodingCandidate> candidates)
    {
        _cache.Set(key, candidates, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = candidates.Count > 0
                ? CandidateCacheDuration
                : EmptyCandidateCacheDuration
        });
        return candidates;
    }

    private static string CreateCacheKey(string provider, string address)
    {
        var normalizedAddress = PublicAreaNameNormalizer.Normalize(address);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAddress));
        return $"property-geocode:{CacheVersion}:{provider}:{Convert.ToHexString(hash)}";
    }

    private static bool IsValidCoordinatePair(decimal latitude, decimal longitude)
    {
        return latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;
    }

}
