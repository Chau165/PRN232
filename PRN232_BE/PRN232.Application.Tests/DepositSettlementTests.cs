using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Policies;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 3.6b (PLAN.md D3 bước 6): deposit settlement at move-out. Every đồng of the deposit is accounted for:
// damage covered + retained for debt + forfeited + refunded = deposit; damage above the deposit is due on top.
public sealed class DepositSettlementTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Theory]
    // deposit, damage, debt, early → covered, retained, forfeited, refund, due
    [InlineData(3_000_000, 1_200_000, 0, false, 1_200_000, 0, 0, 1_800_000, 0)]
    [InlineData(3_000_000, 1_200_000, 0, true, 1_200_000, 0, 1_800_000, 0, 0)]
    [InlineData(3_000_000, 4_000_000, 0, false, 3_000_000, 0, 0, 0, 1_000_000)]
    [InlineData(3_000_000, 0, 500_000, false, 0, 500_000, 0, 2_500_000, 0)]
    [InlineData(3_000_000, 1_000_000, 500_000, true, 1_000_000, 500_000, 1_500_000, 0, 0)]
    [InlineData(3_000_000, 1_000_000, 5_000_000, false, 1_000_000, 2_000_000, 0, 0, 0)]
    public void Every_dong_of_the_deposit_is_accounted_for(int deposit, int damage, int debt, bool early,
        int covered, int retained, int forfeited, int refund, int due)
    {
        var result = DepositSettlementPolicy.Calculate(deposit, damage, debt, early);

        Assert.Equal(new DepositSettlementPolicy.Result(covered, retained, forfeited, refund, due), result);
        Assert.Equal(deposit, result.DamageCovered + result.RetainedForDebt + result.Forfeited + result.Refund);
    }

    [Fact]
    public async Task Settlement_follows_the_move_out_record_and_must_happen_before_checkout()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithAirConditionerAsync(factory, client);
        var url = $"/api/contracts/{contractId}";
        // Notice was given 40 days before moving out today: no forfeiture.
        await UpdateAsync(factory, db =>
        {
            var contract = db.Contracts.Single();
            contract.PlannedMoveOutDate = Today;
            contract.MoveOutNoticeAtUtc = DateTime.UtcNow.AddDays(-40);
        });

        var checkoutFirst = await Terminate(client, url);
        var previewWithoutRecord = await client.GetAsync($"{url}/settlement");
        await client.PutAsJsonAsync($"{url}/handovers/move-in", Handover("Good", null));
        await client.PutAsJsonAsync($"{url}/handovers/move-out", Handover("Damaged", 1_200_000m));
        var preview = await client.GetFromJsonAsync<JsonElement>($"{url}/settlement");
        using var settled = await client.PostAsJsonAsync($"{url}/settlement", new { note = "Đã kiểm tra cùng khách" });
        var again = await client.PostAsJsonAsync($"{url}/settlement", new { });
        var editMoveOut = await client.PutAsJsonAsync($"{url}/handovers/move-out", Handover("Damaged", 100_000m));
        var evidence = await client.GetFromJsonAsync<JsonElement>($"{url}/evidence");
        var checkout = await Terminate(client, url);

        Assert.Equal(HttpStatusCode.Conflict, checkoutFirst.StatusCode);
        Assert.Contains("quyết toán cọc", (await checkoutFirst.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, previewWithoutRecord.StatusCode);
        Assert.False(preview.GetProperty("isSettled").GetBoolean());
        Assert.Equal(1_800_000m, preview.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var settlement = await settled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((true, 3_000_000m, 1_200_000m, 1_800_000m, 0m, false, 40),
            (settlement.GetProperty("isSettled").GetBoolean(), settlement.GetProperty("depositAmount").GetDecimal(),
             settlement.GetProperty("damageCoveredByDeposit").GetDecimal(), settlement.GetProperty("refundAmount").GetDecimal(),
             settlement.GetProperty("forfeitedAmount").GetDecimal(), settlement.GetProperty("earlyWithoutNotice").GetBoolean(),
             settlement.GetProperty("noticeDays").GetInt32()));
        Assert.Equal("Manager A", settlement.GetProperty("settledByName").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, editMoveOut.StatusCode);
        Assert.False(evidence.GetProperty("canUploadMoveOut").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        using var scope = factory.Services.CreateScope();
        var told = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .SingleAsync(item => item.UserId == 10 && item.Title == "Quyết toán cọc phòng A101");
        Assert.Equal("Cọc 3.000.000đ: bồi thường thiết bị 1.200.000đ; hoàn lại 1.800.000đ. Quản lý sẽ chuyển trả trực tiếp cho bạn (hệ thống không chuyển tiền).", told.Body);
    }

    [Fact]
    public async Task Leaving_early_without_notice_forfeits_the_rest_and_only_the_parties_can_see_it()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithAirConditionerAsync(factory, client);
        var url = $"/api/contracts/{contractId}";
        await client.PutAsJsonAsync($"{url}/handovers/move-out", Handover("Good", null));

        using var settled = await client.PostAsJsonAsync($"{url}/settlement", new { });
        await LoginAsync(client, "customer@example.test");
        var customer = await client.GetAsync($"{url}/settlement");
        var bySettleAsCustomer = await client.PostAsJsonAsync($"{url}/settlement", new { });
        await LoginAsync(client, "other@example.test");
        var otherCustomer = await client.GetAsync($"{url}/settlement");

        var settlement = await settled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((true, 3_000_000m, 0m), (settlement.GetProperty("earlyWithoutNotice").GetBoolean(),
            settlement.GetProperty("forfeitedAmount").GetDecimal(), settlement.GetProperty("refundAmount").GetDecimal()));
        Assert.Equal(HttpStatusCode.OK, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bySettleAsCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherCustomer.StatusCode);
    }

    [Fact]
    public async Task Unpaid_invoices_need_acknowledgement_and_keep_the_deposit_against_the_debt()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await InvoicesApiTests.CreateContractWithReadingAsync(client);
        var url = $"/api/contracts/{contractId}";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/invoices", new
        {
            contractId, invoiceMonth = new DateOnly(Today.Year, Today.Month, 1), dueDate = Today.AddDays(10)
        })).StatusCode);
        await client.PutAsJsonAsync($"{url}/handovers/move-out", new { handoverDate = Today, items = Array.Empty<object>() });

        var unacknowledged = await client.PostAsJsonAsync($"{url}/settlement", new { });
        using var acknowledged = await client.PostAsJsonAsync($"{url}/settlement", new { outstandingDebtAcknowledged = true });

        Assert.Equal(HttpStatusCode.BadRequest, unacknowledged.StatusCode);
        Assert.Contains("3.355.000đ", (await unacknowledged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        var settlement = await acknowledged.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((3_355_000m, 3_000_000m, 0m), (settlement.GetProperty("outstandingDebt").GetDecimal(),
            settlement.GetProperty("retainedForDebt").GetDecimal(), settlement.GetProperty("refundAmount").GetDecimal()));
    }

    [Fact]
    public async Task Contracts_under_the_old_terms_have_no_deposit_to_settle()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = true };
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithAirConditionerAsync(factory, client);

        var preview = await client.GetAsync($"/api/contracts/{contractId}/settlement");

        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Contains("không có tiền cọc đảm bảo", (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    // Rebuild 3.6c: money moves outside the system; the manager (or the owning Admin) confirms it once.
    [Fact]
    public async Task Manager_confirms_the_refund_once_and_the_customer_is_told()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithAirConditionerAsync(factory, client);
        var url = $"/api/contracts/{contractId}/settlement";
        await UpdateAsync(factory, db => db.Contracts.Single().MoveOutNoticeAtUtc = DateTime.UtcNow.AddDays(-40));
        await client.PutAsJsonAsync($"/api/contracts/{contractId}/handovers/move-out", Handover("Damaged", 1_200_000m));
        var beforeSettlement = await client.PostAsync($"{url}/refund-confirmed", null);
        await client.PostAsJsonAsync(url, new { });

        await LoginAsync(client, "customer@example.test");
        var byCustomer = await client.PostAsync($"{url}/refund-confirmed", null);
        await LoginAsync(client, "manager-b@example.test");
        var byOtherManager = await client.PostAsync($"{url}/refund-confirmed", null);
        await LoginAsync(client, "manager@example.test");
        var nothingDue = await client.PostAsync($"{url}/due-collected", null);
        using var confirmed = await client.PostAsync($"{url}/refund-confirmed", null);
        var again = await client.PostAsync($"{url}/refund-confirmed", null);

        Assert.Equal(HttpStatusCode.Conflict, beforeSettlement.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherManager.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, nothingDue.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var settlement = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, settlement.GetProperty("refundConfirmedAtUtc").ValueKind);
        Assert.Equal("Manager A", settlement.GetProperty("refundConfirmedByName").GetString());
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AnyAsync(item =>
            item.UserId == 10 && item.Title == "Đã xác nhận hoàn 1.800.000đ tiền cọc phòng A101"));
    }

    [Fact]
    public async Task Only_the_owning_admin_or_manager_confirms_damage_paid_above_the_deposit()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithAirConditionerAsync(factory, client);
        var url = $"/api/contracts/{contractId}/settlement";
        await client.PutAsJsonAsync($"/api/contracts/{contractId}/handovers/move-out", Handover("Damaged", 4_000_000m));
        var settled = await (await client.PostAsJsonAsync(url, new { })).Content.ReadFromJsonAsync<JsonElement>();

        await LoginAsync(client, "admin-b@example.test");
        var notOwner = await client.PostAsync($"{url}/due-collected", null);
        await LoginAsync(client, "admin@example.test");
        using var byOwner = await client.PostAsync($"{url}/due-collected", null);

        Assert.Equal((1_000_000m, 0m), (settled.GetProperty("amountDue").GetDecimal(), settled.GetProperty("refundAmount").GetDecimal()));
        Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        Assert.Equal("Admin", (await byOwner.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dueCollectedByName").GetString());
    }

    private static object Handover(string condition, decimal? charge) => new
    {
        handoverDate = Today,
        items = new[] { new { equipmentId = 601, condition, chargeAmount = charge } }
    };

    private static Task<HttpResponseMessage> Terminate(HttpClient client, string url) =>
        client.PostAsJsonAsync($"{url}/terminate", new { checkoutConfirmed = true, roomReadyForRental = true });

    private static async Task UpdateAsync(BookingsApiFactory factory, Action<AppDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    // Room A101 with one air conditioner worth 8.500.000đ; contract from today; ends logged in as Manager A.
    private static async Task<int> CreateContractWithAirConditionerAsync(BookingsApiFactory factory, HttpClient client)
    {
        await UpdateAsync(factory, db => db.Equipments.Add(new Equipments
        {
            EquipmentId = 601, RoomId = 1, Name = "Máy lạnh", Quantity = 1, CompensationValue = 8_500_000m,
            InstallDate = new DateOnly(2025, 1, 1), WarrantyExpiry = new DateOnly(2027, 1, 1),
            Supplier = "Điện máy Xanh", Status = "Operational"
        }));
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var contract = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, contract.StatusCode);
        return (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }
}
