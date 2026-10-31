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
        var signedPath = json.RootElement.GetProperty("signedURL").GetString()
            ?? throw new InvalidOperationException("Supabase did not return a signed image URL.");
        return Uri.TryCreate(signedPath, UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : $"{_baseUrl ?? throw new InvalidOperationException("Supabase:Url is required.")}/storage/v1{(signedPath.StartsWith('/') ? signedPath : $"/{signedPath}")}";
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
        var baseUrl = _baseUrl ?? throw new InvalidOperationException("Supabase:Url is required for maintenance report image storage.");
        var key = _serviceRoleKey ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is required for maintenance report image storage.");
        var request = new HttpRequestMessage(method, $"{baseUrl}/storage/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private string Bucket => _bucket ?? throw new InvalidOperationException("Supabase:MaintenanceReportBucket is required for maintenance report image storage.");

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
