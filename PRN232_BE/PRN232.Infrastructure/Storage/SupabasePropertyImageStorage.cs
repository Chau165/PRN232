using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Infrastructure.Storage;

public sealed class SupabasePropertyImageStorage : IPropertyImageStorage
{
    private const string DefaultBucket = "property-images";
    private readonly HttpClient _httpClient;
    private readonly string? _baseUrl;
    private readonly string? _serviceRoleKey;
    private readonly string _bucket;

    public SupabasePropertyImageStorage(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Supabase:Url"]?.TrimEnd('/');
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"];
        _bucket = configuration["Supabase:PropertyBucket"]?.Trim() is { Length: > 0 } bucket
            ? bucket
            : DefaultBucket;
    }

    public async Task<string> UploadAsync(
        PropertyImageInput image,
        CancellationToken cancellationToken = default)
    {
        var extension = image.ContentType switch
        {
            "image/jpeg" => Path.GetExtension(image.FileName).ToLowerInvariant(),
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new PropertyImageStorageException()
        };
        var path = $"properties/{Guid.NewGuid():N}{extension}";
        using var request = CreateRequest(
            HttpMethod.Post,
            $"object/{Escape(_bucket)}/{EscapePath(path)}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ByteArrayContent(image.Content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PropertyImageStorageException();
        }

        return $"{BaseUrl}/storage/v1/object/public/{Escape(_bucket)}/{EscapePath(path)}";
    }

    public async Task DeleteAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetObjectPath(imageUrl, out var objectPath))
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Delete, $"object/{Escape(_bucket)}");
        request.Content = JsonContent.Create(new { prefixes = new[] { objectPath } });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PropertyImageStorageException();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var key = _serviceRoleKey;
        if (key is null || string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new PropertyImageStorageException();
        }

        var request = new HttpRequestMessage(method, $"{BaseUrl}/storage/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private string BaseUrl => _baseUrl ?? throw new PropertyImageStorageException();

    private bool TryGetObjectPath(string imageUrl, out string objectPath)
    {
        objectPath = string.Empty;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
            !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) ||
            !string.Equals(imageUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(imageUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = $"/storage/v1/object/public/{Escape(_bucket)}/";
        if (!imageUri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        objectPath = Uri.UnescapeDataString(imageUri.AbsolutePath[prefix.Length..]);
        return objectPath.StartsWith("properties/", StringComparison.Ordinal) &&
               !objectPath.Split('/').Any(segment => segment is "." or "..") &&
               objectPath.Length > "properties/".Length;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
