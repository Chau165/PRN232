using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Storage;

public sealed class SupabaseBookingIdentityImageStorage(HttpClient client, IConfiguration config) : IBookingIdentityImageStorage
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int ReadUrlLifetimeSeconds = 120;
    private string Bucket => config["Supabase:BookingIdentityBucket"]?.Trim() is { Length: > 0 } name
        && Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]{0,62}$") ? name
        : throw new InvalidOperationException("Configure a dedicated private Supabase:BookingIdentityBucket.");
    private Uri BaseUri => Uri.TryCreate(config["Supabase:Url"], UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) ? uri
        : throw new InvalidOperationException("Configure a valid Supabase HTTPS project URL.");
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var key = config["Supabase:ServiceRoleKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Missing Supabase backend credentials.");
        var request = new HttpRequestMessage(method, new Uri(BaseUri, "storage/v1/" + path));
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }
    private async Task EnsurePrivateAsync(CancellationToken token)
    {
        using var request = Request(HttpMethod.Get, "bucket/" + Bucket);
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Cannot verify private identity storage bucket.");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        if (!json.RootElement.TryGetProperty("public", out var value) || value.ValueKind != JsonValueKind.False)
            throw new InvalidOperationException("Identity storage bucket must be private.");
    }
    public async Task<StoredBookingIdentityImage> UploadAsync(int applicationId, int personId, string side,
        BookingIdentityUpload image, CancellationToken cancellationToken = default)
    {
        if (applicationId <= 0 || personId <= 0 || side is not ("Front" or "Back")) throw new ArgumentException("Invalid identity image target.");
        var extension = ValidateImage(image);
        await EnsurePrivateAsync(cancellationToken);
        var path = $"applications/{applicationId}/people/{personId}/{side.ToLowerInvariant()}/{Guid.NewGuid():N}.{extension}";
        using var request = Request(HttpMethod.Post, $"object/{Bucket}/{path}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ByteArrayContent(image.Content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Identity image upload failed.");
        return new(path, image.ContentType, image.Content.LongLength);
    }
    // Checks MIME against file signatures; not OCR, malware scanning or full image decoding.
    private static string ValidateImage(BookingIdentityUpload image)
    {
        var b = image.Content;
        if (b is null || b.Length < 12 || b.Length > MaxBytes) throw new ArgumentException("Identity image must be at most 5 MiB and have a valid image header.");
        return image.ContentType switch
        {
            "image/jpeg" when b[0] == 255 && b[1] == 216 && b[2] == 255 && b[^2] == 255 && b[^1] == 217 => "jpg",
            "image/png" when b.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) => "png",
            "image/webp" when b.AsSpan(0,4).SequenceEqual("RIFF"u8) && b.AsSpan(8,4).SequenceEqual("WEBP"u8) => "webp",
            _ => throw new ArgumentException("Only JPEG, PNG and WebP images matching their MIME type are supported.")
        };
    }
    private static void ValidatePath(string path)
    {
        if (!Regex.IsMatch(path, "^applications/[1-9][0-9]*/people/[1-9][0-9]*/(front|back)/[a-f0-9]{32}\\.(jpg|png|webp)$"))
            throw new ArgumentException("Invalid identity object path.");
    }
    public async Task<string> GetReadUrlAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        ValidatePath(objectPath);
        await EnsurePrivateAsync(cancellationToken);
        using var request = Request(HttpMethod.Post, $"object/sign/{Bucket}/{objectPath}");
        request.Content = JsonContent.Create(new { expiresIn = ReadUrlLifetimeSeconds });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Identity image signing failed.");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("signedURL", out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Invalid signed image URL.");
        var raw = value.GetString();
        var resolved = string.IsNullOrWhiteSpace(raw) ? null : Uri.TryCreate(raw, UriKind.Absolute, out var absolute) ? absolute
            : new Uri(BaseUri, raw.StartsWith("/storage/v1/", StringComparison.Ordinal) ? raw : "/storage/v1/" + raw.TrimStart('/'));
        var expectedPath = $"/storage/v1/object/sign/{Bucket}/{objectPath}";
        if (resolved is null || resolved.Scheme != "https" || resolved.Authority != BaseUri.Authority ||
            !string.IsNullOrEmpty(resolved.UserInfo) || resolved.AbsolutePath != expectedPath ||
            string.IsNullOrEmpty(resolved.Query) || !string.IsNullOrEmpty(resolved.Fragment))
            throw new InvalidOperationException("Invalid signed image URL.");
        return resolved.AbsoluteUri;
    }
    public async Task DeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        ValidatePath(objectPath);
        using var request = Request(HttpMethod.Delete, $"object/{Bucket}");
        request.Content = JsonContent.Create(new { prefixes = new[] { objectPath } });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Identity image deletion failed.");
    }
}
