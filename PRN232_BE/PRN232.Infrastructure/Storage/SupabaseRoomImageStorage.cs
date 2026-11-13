using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Infrastructure.Storage;

public sealed class SupabaseRoomImageStorage : IRoomImageStorage
{
    private const string DefaultBucket = "room-images";
    private const int MaxUploadAttempts = 3;
    private readonly HttpClient _httpClient;
    private readonly string? _baseUrl;
    private readonly string? _serviceRoleKey;
    private readonly string _bucket;
    private readonly ILogger<SupabaseRoomImageStorage> _logger;

    public SupabaseRoomImageStorage(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupabaseRoomImageStorage>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<SupabaseRoomImageStorage>.Instance;
        _baseUrl = configuration["Supabase:Url"]?.TrimEnd('/');
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"];
        _bucket = configuration["Supabase:RoomBucket"]?.Trim() is { Length: > 0 } bucket
            ? bucket
            : DefaultBucket;
    }

    public async Task<string> UploadAsync(
        RoomImageInput image,
        CancellationToken cancellationToken = default)
    {
        var extension = image.ContentType switch
        {
            "image/jpeg" => Path.GetExtension(image.FileName).ToLowerInvariant(),
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new RoomImageStorageException()
        };
        var path = $"rooms/{Guid.NewGuid():N}{extension}";
        for (var attempt = 1; attempt <= MaxUploadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = CreateRequest(HttpMethod.Post, $"object/{Escape(_bucket)}/{EscapePath(path)}");
            // A lost response may follow a successful write. Replay only this upload's random path.
            request.Headers.TryAddWithoutValidation("x-upsert", attempt == 1 ? "false" : "true");
            request.Content = new ByteArrayContent(image.Content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return $"{BaseUrl}/storage/v1/object/public/{Escape(_bucket)}/{EscapePath(path)}";
                }
                _logger.LogWarning("Room image storage returned HTTP {StatusCode} on upload attempt {Attempt} of {MaxAttempts}.",
                    (int)response.StatusCode, attempt, MaxUploadAttempts);
                if (!IsTransient(response.StatusCode) || attempt == MaxUploadAttempts)
                {
                    throw new RoomImageStorageException();
                }
            }
            catch (HttpRequestException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Room image upload connection failed ({HttpRequestError}) on attempt {Attempt} of {MaxAttempts}.",
                    exception.HttpRequestError, attempt, MaxUploadAttempts);
                if (attempt == MaxUploadAttempts)
                {
                    throw new RoomImageStorageException();
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Room image upload timed out on attempt {Attempt} of {MaxAttempts}.", attempt, MaxUploadAttempts);
                if (attempt == MaxUploadAttempts)
                {
                    throw new RoomImageStorageException();
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }
        throw new RoomImageStorageException();
    }

    private static bool IsTransient(HttpStatusCode status) => status is
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or
        HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

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
            throw new RoomImageStorageException();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var key = _serviceRoleKey;
        if (key is null || string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new RoomImageStorageException();
        }

        var request = new HttpRequestMessage(method, $"{BaseUrl}/storage/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private string BaseUrl => _baseUrl ?? throw new RoomImageStorageException();

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
        return imageUrl == $"{BaseUrl}/storage/v1/object/public/{Escape(_bucket)}/{EscapePath(objectPath)}" &&
               objectPath.StartsWith("rooms/", StringComparison.Ordinal) &&
               !objectPath.Split('/').Any(segment => segment is "." or "..") &&
               objectPath.Length > "rooms/".Length;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
