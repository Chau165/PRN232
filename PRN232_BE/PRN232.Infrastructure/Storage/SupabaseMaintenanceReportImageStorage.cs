using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Storage;

public sealed class SupabaseMaintenanceReportImageStorage : IMaintenanceReportImageStorage
{
    private readonly HttpClient _httpClient;
    private readonly string? _baseUrl;
    private readonly string? _serviceRoleKey;
    private readonly string? _bucket;

    public SupabaseMaintenanceReportImageStorage(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Supabase:Url"]?.TrimEnd('/');
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"];
        _bucket = configuration["Supabase:MaintenanceReportBucket"];
    }

    public async Task<string> UploadAsync(int requestId, MaintenanceReportImageInput image)
    {
        var extension = image.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image content type.")
        };
        var path = $"maintenance/{requestId}/{Guid.NewGuid():N}{extension}";
        using var request = CreateRequest(HttpMethod.Post, $"object/{Escape(Bucket)}/{EscapePath(path)}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ByteArrayContent(image.Content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return path;
    }

    public async Task<string> GetReadUrlAsync(string objectPath)
    {
        using var request = CreateRequest(HttpMethod.Post, $"object/sign/{Escape(Bucket)}/{EscapePath(objectPath)}");
        request.Content = JsonContent.Create(new
        {
            expiresIn = 3600
        });
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (!json.RootElement.TryGetProperty("signedURL", out var signedUrlElement) ||
            signedUrlElement.ValueKind != JsonValueKind.String)
        {
            throw InvalidSignedUrlException();
        }

        var signedUrl = signedUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            throw InvalidSignedUrlException();
        }

        return ResolveSignedUrl(signedUrl);
    }

    public async Task DeleteAsync(string objectPath)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"object/{Escape(Bucket)}");
        request.Content = JsonContent.Create(new
        {
            prefixes = new[] { objectPath }
        });
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUrl = _baseUrl;
        if (baseUrl is null)
        {
            throw new InvalidOperationException("Supabase:Url is required for maintenance report image storage.");
        }
        var key = _serviceRoleKey;
        if (key is null)
        {
            throw new InvalidOperationException("Supabase:ServiceRoleKey is required for maintenance report image storage.");
        }
        var request = new HttpRequestMessage(method, $"{baseUrl}/storage/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private string Bucket
    {
        get
        {
            var bucket = _bucket;
            if (bucket is null)
            {
                throw new InvalidOperationException("Supabase:MaintenanceReportBucket is required for maintenance report image storage.");
            }
            return bucket;
        }
    }
    private string ResolveSignedUrl(string signedUrl)
    {
        if (ContainsInvalidUrlCharacters(signedUrl) ||
            !HasValidPercentEscapes(signedUrl) ||
            ContainsEncodedControlCharacters(signedUrl))
        {
            throw InvalidSignedUrlException();
        }

        if (TryGetExplicitScheme(signedUrl, out var scheme, out var colonIndex))
        {
            return ResolveAbsoluteSignedUrl(signedUrl, scheme, colonIndex);
        }

        if (signedUrl.StartsWith("//", StringComparison.Ordinal))
        {
            throw InvalidSignedUrlException();
        }

        return ResolveRelativeSignedUrl(signedUrl);
    }

    private string ResolveAbsoluteSignedUrl(string signedUrl, string scheme, int colonIndex)
    {
        if (signedUrl.IndexOf("://", StringComparison.Ordinal) != colonIndex ||
            !Uri.TryCreate(signedUrl, UriKind.Absolute, out var signedUri) ||
            string.IsNullOrEmpty(signedUri.Host) ||
            signedUri.UserInfo.Length > 0)
        {
            throw InvalidSignedUrlException();
        }

        var signedPath = GetAbsolutePath(signedUrl, colonIndex);
        if (ContainsPathTraversal(signedPath))
        {
            throw InvalidSignedUrlException();
        }

        if (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(signedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return signedUrl;
        }

        if (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            IsAllowedLoopbackHttp(signedUri))
        {
            return signedUrl;
        }

        throw InvalidSignedUrlException();
    }

    private string ResolveRelativeSignedUrl(string signedUrl)
    {
        var queryIndex = signedUrl.IndexOf('?');
        string signedPath;
        string query;
        if (queryIndex < 0)
        {
            signedPath = signedUrl;
            query = string.Empty;
        }
        else
        {
            signedPath = signedUrl[..queryIndex];
            query = signedUrl[queryIndex..];
        }
        if (signedPath.StartsWith("/", StringComparison.Ordinal))
        {
            signedPath = signedPath[1..];
        }

        if (ContainsPathTraversal(signedPath))
        {
            throw InvalidSignedUrlException();
        }

        string storagePath;
        if (signedPath.StartsWith("storage/v1/object/sign/", StringComparison.Ordinal))
        {
            storagePath = signedPath;
        }
        else if (signedPath.StartsWith("object/sign/", StringComparison.Ordinal))
        {
            storagePath = $"storage/v1/{signedPath}";
        }
        else
        {
            throw InvalidSignedUrlException();
        }

        var baseUri = GetConfiguredBaseUri();
        if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !baseUri.IsLoopback)
        {
            throw InvalidSignedUrlException();
        }

        var resolvedUrl = $"{_baseUrl}/{storagePath}{query}";
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var resolvedUri) ||
            (string.Equals(resolvedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !IsAllowedLoopbackHttp(resolvedUri)))
        {
            throw InvalidSignedUrlException();
        }

        return resolvedUrl;
    }

    private bool IsAllowedLoopbackHttp(Uri signedUri)
    {
        if (!string.Equals(signedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseUri = GetConfiguredBaseUri();
        return string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
               baseUri.IsLoopback &&
               signedUri.IsLoopback &&
               string.Equals(baseUri.Host, signedUri.Host, StringComparison.OrdinalIgnoreCase) &&
               baseUri.Port == signedUri.Port;
    }

    private Uri GetConfiguredBaseUri()
    {
        if (_baseUrl is null ||
            !Uri.TryCreate(_baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(baseUri.Host) ||
            baseUri.UserInfo.Length > 0 ||
            baseUri.Query.Length > 0 ||
            baseUri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Supabase:Url must be an absolute HTTP or HTTPS URL.");
        }

        return baseUri;
    }

    private static bool TryGetExplicitScheme(string value, out string scheme, out int colonIndex)
    {
        colonIndex = value.IndexOf(':');
        scheme = string.Empty;
        if (colonIndex <= 0)
        {
            return false;
        }

        var firstPathOrQueryIndex = value.IndexOfAny(new[] { '/', '?' });
        if (firstPathOrQueryIndex >= 0 && firstPathOrQueryIndex < colonIndex)
        {
            return false;
        }

        var candidate = value[..colonIndex];
        if (!char.IsAsciiLetter(candidate[0]))
        {
            return false;
        }

        for (var index = 1; index < candidate.Length; index++)
        {
            var character = candidate[index];
            if (!char.IsAsciiLetterOrDigit(character) && character != '+' && character != '.' && character != '-')
            {
                return false;
            }
        }

        scheme = candidate;
        return true;
    }

    private static string GetAbsolutePath(string signedUrl, int colonIndex)
    {
        var authorityStart = colonIndex + 3;
        var pathStart = signedUrl.IndexOf('/', authorityStart);
        var queryStart = signedUrl.IndexOf('?', authorityStart);
        if (pathStart < 0 || (queryStart >= 0 && queryStart < pathStart))
        {
            return string.Empty;
        }

        var pathEnd = signedUrl.Length;
        if (queryStart >= 0)
        {
            pathEnd = queryStart;
        }
        return signedUrl[pathStart..pathEnd];
    }

    private static bool ContainsInvalidUrlCharacters(string value)
    {
        return value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) ||
               value.Contains('\\') ||
               value.Contains('#');
    }

    private static bool HasValidPercentEscapes(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length ||
                !Uri.IsHexDigit(value[index + 1]) ||
                !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }

    private static bool ContainsEncodedControlCharacters(string value)
    {
        var decodedValue = value;
        while (true)
        {
            var nextDecodedValue = Uri.UnescapeDataString(decodedValue);
            if (nextDecodedValue.Any(char.IsControl))
            {
                return true;
            }
            if (nextDecodedValue == decodedValue)
            {
                return false;
            }
            decodedValue = nextDecodedValue;
        }
    }

    private static bool ContainsPathTraversal(string encodedPath)
    {
        var path = encodedPath;
        while (true)
        {
            if (path.Contains('\\') || path.Split('/').Any(segment => segment is "." or ".."))
            {
                return true;
            }

            var decodedPath = Uri.UnescapeDataString(path);
            if (decodedPath == path)
            {
                return false;
            }

            path = decodedPath;
        }
    }

    private static InvalidOperationException InvalidSignedUrlException()
    {
        return new InvalidOperationException("Supabase did not return a valid signed image URL.");
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string EscapePath(string path)
    {
        return string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    }
}
