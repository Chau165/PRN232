using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 3.5b/3.5c: both parties upload evidence without approval; each deletes only their own files within
// the window; move-in photos close 7 days after move-in; Admin can only hide files, with a reason.
public sealed class ContractEvidenceApiTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n%%EOF\n");

    [Fact]
    public async Task Both_parties_upload_without_approval_and_only_delete_their_own_files()
    {
        var storage = new MemoryEvidenceStorage();
        using var factory = new BookingsApiFactory();
        using var app = WithStorage(factory, storage);
        using var client = app.CreateClient();
        var contractId = await CreateContractAsync(client);
        var url = $"/api/contracts/{contractId}/evidence";

        var byManager = await UploadAsync(client, url, "MoveIn", Png, "image/png", "may-lanh.png");
        await LoginAsync(client, "customer@example.test");
        var byCustomer = await UploadAsync(client, url, "MoveIn", Png, "image/png", "vet-nut-tuong.png");
        var signed = await UploadAsync(client, url, "SignedContract", Pdf, "application/pdf", "hop-dong.pdf");
        var files = (await byCustomer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("files");
        var managerFileId = files[0].GetProperty("contractEvidenceFileId").GetInt32();
        var customerFileId = files[1].GetProperty("contractEvidenceFileId").GetInt32();
        var deleteOthers = await client.DeleteAsync($"{url}/{managerFileId}");
        using var deleteOwn = await client.DeleteAsync($"{url}/{customerFileId}");

        Assert.Equal(HttpStatusCode.OK, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signed.StatusCode);
        Assert.Equal(("Manager A", "Manager", false), (files[0].GetProperty("uploadedByName").GetString(),
            files[0].GetProperty("uploadedByRole").GetString(), files[0].GetProperty("canDelete").GetBoolean()));
        Assert.Equal(("Customer A", true), (files[1].GetProperty("uploadedByName").GetString(), files[1].GetProperty("canDelete").GetBoolean()));
        Assert.StartsWith("https://signed.test/", files[0].GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, deleteOthers.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteOwn.StatusCode);
        var remaining = (await deleteOwn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("files");
        Assert.Equal(["MoveIn", "SignedContract"], remaining.EnumerateArray().Select(file => file.GetProperty("kind").GetString()));
        Assert.Equal(2, storage.Objects.Count);
    }

    [Fact]
    public async Task Admin_hides_with_a_reason_and_the_parties_no_longer_get_a_link()
    {
        using var factory = new BookingsApiFactory();
        using var app = WithStorage(factory, new MemoryEvidenceStorage());
        using var client = app.CreateClient();
        var contractId = await CreateContractAsync(client);
        var url = $"/api/contracts/{contractId}/evidence";
        var uploaded = await (await UploadAsync(client, url, "MoveOut", Png, "image/png", "anh.png")).Content.ReadFromJsonAsync<JsonElement>();
        var fileId = uploaded.GetProperty("files")[0].GetProperty("contractEvidenceFileId").GetInt32();

        await LoginAsync(client, "admin@example.test");
        var adminUpload = await UploadAsync(client, url, "MoveOut", Png, "image/png", "admin.png");
        var noReason = await client.PostAsJsonAsync($"{url}/{fileId}/hide", new { reason = " " });
        using var hidden = await client.PostAsJsonAsync($"{url}/{fileId}/hide", new { reason = "Ảnh không liên quan đến phòng" });
        var adminView = (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("files")[0];
        await LoginAsync(client, "customer@example.test");
        var customerView = (await client.GetFromJsonAsync<JsonElement>(url)).GetProperty("files")[0];

        Assert.Equal(HttpStatusCode.Forbidden, adminUpload.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, adminView.GetProperty("url").ValueKind);
        Assert.True(customerView.GetProperty("isHidden").GetBoolean());
        Assert.Equal(JsonValueKind.Null, customerView.GetProperty("url").ValueKind);
        Assert.Equal("Ảnh không liên quan đến phòng", customerView.GetProperty("hiddenReason").GetString());
    }

    [Fact]
    public async Task Move_in_photos_close_seven_days_after_move_in_and_fake_files_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var app = WithStorage(factory, new MemoryEvidenceStorage());
        using var client = app.CreateClient();
        var contractId = await CreateContractAsync(client);
        var url = $"/api/contracts/{contractId}/evidence";

        var renamedText = await UploadAsync(client, url, "MoveIn", Encoding.UTF8.GetBytes("not really a png"), "image/png", "anh.png");
        using (var scope = app.Services.CreateScope())
        {
            // Moved in 8 days ago: the 7-day window for move-in photos is over.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Contracts.SingleAsync()).StartDate = Today.AddDays(-8);
            await db.SaveChangesAsync();
        }
        var lateMoveIn = await UploadAsync(client, url, "MoveIn", Png, "image/png", "anh.png");
        var moveOut = await UploadAsync(client, url, "MoveOut", Png, "image/png", "anh.png");
        var evidence = await client.GetFromJsonAsync<JsonElement>(url);
        await LoginAsync(client, "other@example.test");
        var otherCustomer = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, renamedText.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lateMoveIn.StatusCode);
        Assert.Contains($"hạn đến hết ngày {Today.AddDays(-1).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}",
            (await lateMoveIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, moveOut.StatusCode);
        Assert.Equal((false, true), (evidence.GetProperty("canUploadMoveIn").GetBoolean(), evidence.GetProperty("canUploadMoveOut").GetBoolean()));
        Assert.Equal(HttpStatusCode.Forbidden, otherCustomer.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, string kind, byte[] content,
        string contentType, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await client.PostAsync($"{url}?kind={kind}", form);
    }

    // Ends logged in as Manager A, who manages the contract's property.
    private static async Task<int> CreateContractAsync(HttpClient client)
    {
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var contract = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, contract.StatusCode);
        return (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<PRN232.Api.Controllers.BookingsController> WithStorage(
        BookingsApiFactory factory, MemoryEvidenceStorage storage) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IContractEvidenceStorage>();
            services.AddSingleton<IContractEvidenceStorage>(storage);
        }));

    private sealed class MemoryEvidenceStorage : IContractEvidenceStorage
    {
        public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

        public Task<string> UploadAsync(int contractId, string kind, string contentType, byte[] content)
        {
            var path = $"contracts/{contractId}/{kind.ToLowerInvariant()}/{Guid.NewGuid():N}";
            Objects[path] = content;
            return Task.FromResult(path);
        }

        public Task<string> GetReadUrlAsync(string objectPath) => Task.FromResult($"https://signed.test/{objectPath}?token=t");

        public Task DeleteAsync(string objectPath)
        {
            Objects.TryRemove(objectPath, out _);
            return Task.CompletedTask;
        }
    }
}
