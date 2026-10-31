using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Storage;
using Xunit;

namespace PRN232.Application.Tests;

public class SupabaseMaintenanceReportImageStorageTests
{
    [Fact]
    public async Task Upload_persists_object_path_and_signing_returns_temporary_url()
    {
        var handler = new StorageHandler();
        using var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://project.supabase.co",
            ["Supabase:ServiceRoleKey"] = "test-only-storage-key",
            ["Supabase:MaintenanceReportBucket"] = "maintenance-evidence"
        }).Build();
        var storage = new SupabaseMaintenanceReportImageStorage(client, configuration);

        var path = await storage.UploadAsync(12,
            new MaintenanceReportImageInput("client-name.png", "image/png", [1, 2, 3]));
        var signedUrl = await storage.GetReadUrlAsync(path);
        await storage.DeleteAsync(path);

        Assert.StartsWith("maintenance/12/", path);
        Assert.EndsWith(".png", path);
        Assert.Equal("https://project.supabase.co/storage/v1/object/sign/maintenance-evidence/" + path + "?token=test",
            signedUrl);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("/storage/v1/object/maintenance-evidence/maintenance/12/", handler.Requests[0].Url);
        Assert.Equal("image/png", handler.Requests[0].ContentType);
        Assert.Equal("test-only-storage-key", handler.Requests[0].ApiKey);
        Assert.Contains("expiresIn", handler.Requests[1].Body);
        Assert.Contains(path, handler.Requests[2].Body);
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/webp", ".webp")]
    public async Task Storage_uses_allowed_mime_extension_and_random_server_object_names(string type, string extension)
    {
        var handler = new StorageHandler();
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());
        var first = await storage.UploadAsync(12, new MaintenanceReportImageInput("../../client-name", type, [1]));
        var second = await storage.UploadAsync(12, new MaintenanceReportImageInput("../../client-name", type, [1]));
        Assert.EndsWith(extension, first);
        Assert.StartsWith("maintenance/12/", first);
        Assert.DoesNotContain("client-name", first);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("Supabase:Url")]
    [InlineData("Supabase:ServiceRoleKey")]
    [InlineData("Supabase:MaintenanceReportBucket")]
    public async Task Storage_requires_server_configuration(string missing)
    {
        using var client = new HttpClient(new StorageHandler());
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration(missing));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.UploadAsync(12, new MaintenanceReportImageInput("evidence.png", "image/png", [1])));
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("sign")]
    [InlineData("delete")]
    public async Task Storage_http_failures_are_propagated(string operation)
    {
        using var client = new HttpClient(new StorageHandler { Fail = true });
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            switch (operation)
            {
                case "upload":
                    await storage.UploadAsync(12, new MaintenanceReportImageInput("evidence.png", "image/png", [1]));
                    break;
                case "sign":
                    await storage.GetReadUrlAsync("maintenance/12/evidence.png");
                    break;
                case "delete":
                    await storage.DeleteAsync("maintenance/12/evidence.png");
                    break;
            }
        });
    }

    [Fact]
    public async Task Signed_url_expiry_is_one_hour_and_absolute_urls_are_supported()
    {
        var handler = new StorageHandler { SignedUrl = "https://storage.test/signed?token=test" };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());
        Assert.Equal("https://storage.test/signed?token=test", await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(3600, body.RootElement.GetProperty("expiresIn").GetInt32());
    }

    [Theory]
    [InlineData(
        "/object/sign/maintenance-evidence/report%20one%2Ffinal.png?token=a%2Bb%2Fc%3D&download=1+2",
        "https://project.supabase.co/storage/v1/object/sign/maintenance-evidence/report%20one%2Ffinal.png?token=a%2Bb%2Fc%3D&download=1+2")]
    [InlineData(
        "object/sign/maintenance-evidence/report.png?token=test",
        "https://project.supabase.co/storage/v1/object/sign/maintenance-evidence/report.png?token=test")]
    [InlineData(
        "/storage/v1/object/sign/maintenance-evidence/report.png?token=test",
        "https://project.supabase.co/storage/v1/object/sign/maintenance-evidence/report.png?token=test")]
    [InlineData(
        "storage/v1/object/sign/maintenance-evidence/report.png?token=test",
        "https://project.supabase.co/storage/v1/object/sign/maintenance-evidence/report.png?token=test")]
    public async Task Relative_signed_urls_preserve_path_and_query_without_duplicate_prefix(
        string signedUrl,
        string expectedUrl)
    {
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());

        Assert.Equal(expectedUrl, await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
    }

    [Fact]
    public async Task Absolute_https_signed_url_is_returned_without_changing_encoding_or_query()
    {
        const string signedUrl = "https://storage.test/object/sign/report%2Ffinal.png?token=a%2Bb%2Fc%3D&other=x+y&flag=1";
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());

        Assert.Equal(signedUrl, await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
    }

    [Fact]
    public async Task Http_loopback_signed_url_is_allowed_when_base_url_is_configured_for_same_loopback_origin()
    {
        const string baseUrl = "http://127.0.0.1:54321";
        const string signedUrl = "http://127.0.0.1:54321/storage/v1/object/sign/report.png?token=test";
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration(supabaseUrl: baseUrl));

        Assert.Equal(signedUrl, await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
    }

    [Fact]
    public async Task Relative_signed_url_uses_configured_http_loopback_base_url()
    {
        const string baseUrl = "http://localhost:54321";
        const string signedUrl = "/object/sign/maintenance-evidence/report.png?token=test";
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration(supabaseUrl: baseUrl));

        Assert.Equal("http://localhost:54321/storage/v1/object/sign/maintenance-evidence/report.png?token=test",
            await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
    }

    [Theory]
    [InlineData("https://project.supabase.co", "http://127.0.0.1:54321/object/sign/report.png?token=secret")]
    [InlineData("http://storage.test", "http://storage.test/object/sign/report.png?token=secret")]
    [InlineData("http://localhost:54321", "http://127.0.0.1:54321/object/sign/report.png?token=secret")]
    public async Task Http_signed_urls_are_rejected_unless_configured_base_is_the_same_loopback_origin(
        string baseUrl,
        string signedUrl)
    {
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration(supabaseUrl: baseUrl));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.GetReadUrlAsync("maintenance/12/evidence.png"));

        Assert.DoesNotContain("test-only-storage-key", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file:///object/sign/report.png?token=secret")]
    [InlineData("ftp://storage.test/object/sign/report.png?token=secret")]
    [InlineData("javascript:alert(1)")]
    [InlineData("custom:unsupported-value")]
    [InlineData("//storage.test/object/sign/report.png?token=secret")]
    [InlineData(@"\\storage.test\share\report.png")]
    [InlineData("C:/object/sign/report.png")]
    [InlineData("/object/sign/../private.png?token=secret")]
    [InlineData("/object/sign/%2e%2e/private.png?token=secret")]
    [InlineData("/object/sign/report%0A.png?token=secret")]
    [InlineData("/object/sign/report.png?token=secret%0A")]
    [InlineData("https://storage.test/object/sign/../private.png?token=secret")]
    [InlineData("/object/sign/report.png?token=secret#fragment")]
    public async Task Unsupported_or_unsafe_signed_urls_are_rejected_without_exposing_secrets(string signedUrl)
    {
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.GetReadUrlAsync("maintenance/12/evidence.png"));

        Assert.DoesNotContain("test-only-storage-key", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signed_url_with_control_character_is_rejected_without_exposing_token()
    {
        var signedUrl = "/object/sign/report.png?token=secret" + '\u0001';
        var handler = new StorageHandler { SignedUrl = signedUrl };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.GetReadUrlAsync("maintenance/12/evidence.png"));

        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"signedURL\":null}")]
    [InlineData("{\"signedURL\":\"\"}")]
    [InlineData("{\"signedURL\":\"   \"}")]
    [InlineData("{\"signedURL\":42}")]
    public async Task Missing_null_empty_or_non_string_signed_url_is_rejected(string responseBody)
    {
        var handler = new StorageHandler { ResponseBody = responseBody };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.GetReadUrlAsync("maintenance/12/evidence.png"));

        Assert.DoesNotContain("test-only-storage-key", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    private static Microsoft.Extensions.Configuration.IConfiguration Configuration(
        string? missing = null,
        string supabaseUrl = "https://project.supabase.co")
    {
        var settings = new Dictionary<string, string?>
        {
            ["Supabase:Url"] = supabaseUrl,
            ["Supabase:ServiceRoleKey"] = "test-only-storage-key",
            ["Supabase:MaintenanceReportBucket"] = "maintenance-evidence"
        };
        if (missing is not null)
        {
            settings.Remove(missing);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class StorageHandler : HttpMessageHandler
    {
        public bool Fail
        {
            get; init;
        }
        public string? SignedUrl
        {
            get; init;
        }
        public string? ResponseBody
        {
            get; init;
        }
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.RequestUri!.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.GetValues("apikey").Single(),
                body));

            if (Fail)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            var isSigningRequest = request.RequestUri.AbsolutePath.Contains("object/sign", StringComparison.Ordinal);
            var content = ResponseBody ?? (SignedUrl is not null
                ? JsonSerializer.Serialize(new { signedURL = SignedUrl })
                : isSigningRequest
                    ? "{\"signedURL\":\"/object/sign/maintenance-evidence/" +
                      Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Split("object/sign/maintenance-evidence/", StringSplitOptions.None)[1]) +
                      "?token=test\"}"
                    : "{}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(string Url, string? ContentType, string ApiKey, string Body);
}
