using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 3.6a: moving out before the contract end needs at least 30 days' notice, otherwise the rest of the
// deposit is forfeited at settlement. The notice tells the other party and shows the consequence up front.
public sealed class MoveOutNoticeApiTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Fact]
    public async Task Short_notice_warns_that_the_deposit_is_forfeited_and_tells_the_manager()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        await LoginAsync(client, "customer@example.test");

        using var shortNotice = await client.PutAsJsonAsync($"/api/contracts/{contractId}/move-out-notice",
            new { plannedMoveOutDate = Today.AddDays(20) });
        var contract = await shortNotice.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, shortNotice.StatusCode);
        Assert.Equal((Today.AddDays(20).ToString("yyyy-MM-dd"), 20, true), (contract.GetProperty("plannedMoveOutDate").GetString(),
            contract.GetProperty("moveOutNoticeDays").GetInt32(), contract.GetProperty("moveOutForfeitsDeposit").GetBoolean()));
        using var scope = factory.Services.CreateScope();
        var toManager = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .SingleAsync(item => item.UserId == 7 && item.Type == "Contract");
        Assert.Equal($"Báo trả phòng A101 ngày {Today.AddDays(20).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}", toManager.Title);
        Assert.Contains("mất phần tiền cọc còn lại", toManager.Body);
    }

    [Fact]
    public async Task Thirty_days_notice_keeps_the_deposit_and_the_notice_can_be_cancelled()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        var url = $"/api/contracts/{contractId}/move-out-notice";

        // The manager records the customer's notice on their behalf.
        var given = await (await client.PutAsJsonAsync(url, new { plannedMoveOutDate = Today.AddDays(30) }))
            .Content.ReadFromJsonAsync<JsonElement>();
        using var cancelled = await client.DeleteAsync(url);
        var cancelAgain = await client.DeleteAsync(url);

        Assert.False(given.GetProperty("moveOutForfeitsDeposit").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("plannedMoveOutDate").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, cancelAgain.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .CountAsync(item => item.UserId == 10 && item.Type == "Contract" && item.RelatedEntityId == contractId));
    }

    [Fact]
    public async Task Dates_outside_the_contract_and_outsiders_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        var url = $"/api/contracts/{contractId}/move-out-notice";
        await LoginAsync(client, "customer@example.test");

        var past = await client.PutAsJsonAsync(url, new { plannedMoveOutDate = Today.AddDays(-1) });
        var afterEnd = await client.PutAsJsonAsync(url, new { plannedMoveOutDate = Today.AddYears(1).AddDays(1) });
        await LoginAsync(client, "other@example.test");
        var otherCustomer = await client.PutAsJsonAsync(url, new { plannedMoveOutDate = Today.AddDays(40) });
        await LoginAsync(client, "admin@example.test");
        var admin = await client.PutAsJsonAsync(url, new { plannedMoveOutDate = Today.AddDays(40) });

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, afterEnd.StatusCode);
        Assert.Contains("gia hạn hợp đồng", (await afterEnd.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, otherCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    // Contract from today for one year; ends logged in as Manager A.
    private static async Task<int> CreateContractAsync(HttpClient client)
    {
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var contract = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, contract.StatusCode);
        return (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }
}
