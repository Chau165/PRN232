using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 4.1 (PLAN.md D4): occupants share the holder's contract. Their CCCD is required, encrypted at rest,
// never returned in full by list APIs, and only the assigned manager or owning Admin can reveal it — each reveal
// is written to the audit log.
public sealed class ContractOccupantApiTests
{
    private const string Cccd = "079201001234";
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Fact]
    public async Task Holder_adds_a_roommate_whose_identity_is_masked_and_encrypted_at_rest()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var url = await CreateContractAsync(factory, client, maxOccupants: 2);
        await LoginAsync(client, "customer@example.test");

        using var added = await client.PostAsJsonAsync(url, Roommate("Trần Thị B", Cccd));
        var raw = await added.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.DoesNotContain(Cccd, raw, StringComparison.Ordinal);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal((2, 2, true, false), (body.GetProperty("currentCount").GetInt32(), body.GetProperty("maxOccupants").GetInt32(),
            body.GetProperty("canManage").GetBoolean(), body.GetProperty("canRevealIdentity").GetBoolean()));
        Assert.Equal("********1234", body.GetProperty("occupants")[0].GetProperty("maskedIdentityNumber").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Database.SqlQueryRaw<string>("SELECT \"IdentityNumber\" AS \"Value\" FROM \"ContractOccupants\"").SingleAsync();
        Assert.StartsWith("CfDJ8", stored);
        Assert.DoesNotContain(Cccd, stored, StringComparison.Ordinal);
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == 7 && item.Title == "Thêm người ở cùng phòng A101: Trần Thị B"));
    }

    [Fact]
    public async Task The_room_limit_counts_the_holder_and_frees_up_when_someone_moves_out()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var url = await CreateContractAsync(factory, client, maxOccupants: 2);

        var first = await client.PostAsJsonAsync(url, Roommate("Trần Thị B", Cccd));
        var overLimit = await client.PostAsJsonAsync(url, Roommate("Lê Văn C", "079201005678"));
        var occupantId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("occupants")[0].GetProperty("contractOccupantId").GetInt32();
        using var movedOut = await client.PutAsJsonAsync($"{url}/{occupantId}/move-out", new { moveOutDate = Today });
        var afterMoveOut = await client.PostAsJsonAsync(url, Roommate("Lê Văn C", "079201005678"));
        var movedOutAgain = await client.PutAsJsonAsync($"{url}/{occupantId}/move-out", new { moveOutDate = Today });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
        Assert.Contains("Phòng A101 chỉ được ở tối đa 2 người.", (await overLimit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.False((await movedOut.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("occupants")[0].GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, afterMoveOut.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, movedOutAgain.StatusCode);
    }

    [Fact]
    public async Task Only_the_manager_or_owning_admin_reveals_the_full_identity_and_each_reveal_is_logged()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var url = await CreateContractAsync(factory, client, maxOccupants: 2);
        var occupantId = (await (await client.PostAsJsonAsync(url, Roommate("Trần Thị B", Cccd))).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("occupants")[0].GetProperty("contractOccupantId").GetInt32();
        var reveal = $"{url}/{occupantId}/reveal-identity";

        using var byManager = await client.PostAsync(reveal, null);
        await LoginAsync(client, "customer@example.test");
        var byHolder = await client.PostAsync(reveal, null);
        await LoginAsync(client, "admin-b@example.test");
        var byOtherAdmin = await client.PostAsync(reveal, null);
        await LoginAsync(client, "admin@example.test");
        var byOwner = await client.PostAsync(reveal, null);

        Assert.Equal(Cccd, (await byManager.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identityNumber").GetString());
        Assert.Contains("no-store", byManager.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, byHolder.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        using var scope = factory.Services.CreateScope();
        var reveals = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .Where(log => log.Action == "ContractOccupants.RevealIdentity" && log.Succeeded)
            .Select(log => log.UserId).ToListAsync();
        Assert.Equal([7, 9], reveals.Order());
    }

    [Fact]
    public async Task Identity_number_must_be_twelve_digits()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var url = await CreateContractAsync(factory, client, maxOccupants: 2);

        using var response = await client.PostAsJsonAsync(url, Roommate("Trần Thị B", "07920100123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Số CCCD phải gồm đúng 12 chữ số.",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("identityNumber")[0].GetString());
    }

    private static object Roommate(string name, string cccd) =>
        new { fullName = name, phoneNumber = "0905123456", identityNumber = cccd, moveInDate = Today };

    // Contract on room A101 from today; ends logged in as Manager A. Returns the occupants URL.
    private static async Task<string> CreateContractAsync(BookingsApiFactory factory, HttpClient client, int maxOccupants)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rooms.SingleAsync(room => room.RoomId == 1)).MaxOccupants = maxOccupants;
            await db.SaveChangesAsync();
        }
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var contract = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, contract.StatusCode);
        var contractId = (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
        return $"/api/contracts/{contractId}/occupants";
    }
}
