using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Storage;

// Rebuild 3.5b: bucket PRIVATE "Supabase:ContractEvidenceBucket" (vd. contract-evidence), truy cập bằng service
// role key. Người xem chỉ nhận link ký tạm 1 giờ, giống ảnh báo hỏng hóc (SupabaseMaintenanceReportImageStorage).
public sealed class SupabaseContractEvidenceStorage(HttpClient httpClient, IConfiguration configuration) : IContractEvidenceStorage
{
    private const int SignedUrlSeconds = 3600;
    private readonly string? _baseUrl = configuration["Supabase:Url"]?.TrimEnd('/');
    private readonly string? _serviceRoleKey = configuration["Supabase:ServiceRoleKey"];
    private readonly string? _bucket = configuration["Supabase:ContractEvidenceBucket"];

    public async Task<string> UploadAsync(int contractId, string kind, string contentType, byte[] content)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => throw new InvalidOperationException("Unsupported evidence content type.")
        };
        var path = $"contracts/{contractId}/{kind.ToLowerInvariant()}/{Guid.NewGuid():N}{extension}";
        using var request = CreateRequest(HttpMethod.Post, $"object/{Escape(Bucket)}/{EscapePath(path)}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return path;
    }

    public async Task<string> GetReadUrlAsync(string objectPath)
    {
        using var request = CreateRequest(HttpMethod.Post, $"object/sign/{Escape(Bucket)}/{EscapePath(objectPath)}");
        request.Content = JsonContent.Create(new { expiresIn = SignedUrlSeconds });
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var signed = json.RootElement.TryGetProperty("signedURL", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        return ResolveSignedUrl(signed);
    }

    public async Task DeleteAsync(string objectPath)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"object/{Escape(Bucket)}");
        request.Content = JsonContent.Create(new { prefixes = new[] { objectPath } });
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // Supabase trả đường dẫn tương đối "/object/sign/{bucket}/{path}?token=...". Chỉ chấp nhận đúng dạng đó và
    // ghép với Supabase:Url của mình, để link trả cho người dùng không thể trỏ sang máy chủ khác.
    private string ResolveSignedUrl(string? signed)
    {
        if (string.IsNullOrWhiteSpace(signed) || signed.Contains("..") || signed.Contains('\\') ||
            signed.Any(char.IsWhiteSpace) || signed.Any(char.IsControl))
        {
            throw InvalidSignedUrl();
        }
        var relative = signed.TrimStart('/');
        if (relative.StartsWith("storage/v1/", StringComparison.Ordinal))
        {
            relative = relative["storage/v1/".Length..];
        }
        if (!relative.StartsWith($"object/sign/{Escape(Bucket)}/", StringComparison.Ordinal) &&
            !relative.StartsWith($"object/sign/{Bucket}/", StringComparison.Ordinal))
        {
            throw InvalidSignedUrl();
        }
        return $"{BaseUrl}/storage/v1/{relative}";
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var key = _serviceRoleKey ?? throw new InvalidOperationException(
            "Supabase:ServiceRoleKey is required for contract evidence storage.");
        var request = new HttpRequestMessage(method, $"{BaseUrl}/storage/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private string BaseUrl => _baseUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                              (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback)
        ? url
        : throw new InvalidOperationException("Supabase:Url must be an absolute HTTPS URL for contract evidence storage.");

    private string Bucket => _bucket ?? throw new InvalidOperationException(
        "Supabase:ContractEvidenceBucket is required for contract evidence storage.");

    private static InvalidOperationException InvalidSignedUrl() =>
        new("Supabase did not return a valid signed contract evidence URL.");

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
