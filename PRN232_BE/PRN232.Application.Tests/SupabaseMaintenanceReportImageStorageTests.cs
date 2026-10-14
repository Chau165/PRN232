using System.Net;
using System.Text;
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
                case "upload": await storage.UploadAsync(12, new MaintenanceReportImageInput("evidence.png", "image/png", [1])); break;
                case "sign": await storage.GetReadUrlAsync("maintenance/12/evidence.png"); break;
                case "delete": await storage.DeleteAsync("maintenance/12/evidence.png"); break;
            }
        });
    }

    [Fact]
    public async Task Signed_url_expiry_is_one_hour_and_absolute_urls_are_supported()
    {
        var handler = new StorageHandler { AbsoluteSignedUrl = true };
        using var client = new HttpClient(handler);
        var storage = new SupabaseMaintenanceReportImageStorage(client, Configuration());
        Assert.Equal("https://storage.test/signed?token=test", await storage.GetReadUrlAsync("maintenance/12/evidence.png"));
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(3600, body.RootElement.GetProperty("expiresIn").GetInt32());
    }

    private static Microsoft.Extensions.Configuration.IConfiguration Configuration(string? missing = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://project.supabase.co", ["Supabase:ServiceRoleKey"] = "test-only-storage-key",
            ["Supabase:MaintenanceReportBucket"] = "maintenance-evidence"
        };
        if (missing is not null) settings.Remove(missing);
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class StorageHandler : HttpMessageHandler
    {
        public bool Fail { get; init; }
        public bool AbsoluteSignedUrl { get; init; }
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.RequestUri!.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.GetValues("apikey").Single(),
                body));

            if (Fail) return new HttpResponseMessage(HttpStatusCode.BadGateway);
            if (AbsoluteSignedUrl) return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"signedURL\":\"https://storage.test/signed?token=test\"}", Encoding.UTF8, "application/json")
            };

            var content = request.RequestUri.AbsolutePath.Contains("object/sign", StringComparison.Ordinal)
                ? "{\"signedURL\":\"/object/sign/maintenance-evidence/" +
                  Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Split("object/sign/maintenance-evidence/", StringSplitOptions.None)[1]) +
                  "?token=test\"}"
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(string Url, string? ContentType, string ApiKey, string Body);
}
