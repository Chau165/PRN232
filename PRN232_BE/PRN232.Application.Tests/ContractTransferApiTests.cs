using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 4.2 (PLAN.md D4): the holder moves out, a roommate stays. The old holder and the new holder confirm,
// then the manager completes: the old contract ends the day before the effective date and a new contract starts
// with the same rent and end date, the whole deposit, continuous meter readings, the inspection as its move-in
// record and the remaining roommates.
public sealed class ContractTransferApiTests
{
    private const string Url = "/api/contract-transfers";
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static readonly DateOnly ThisMonth = new(Today.Year, Today.Month, 1);
    private static readonly DateOnly LastMonth = ThisMonth.AddMonths(-1);

    [Fact]
    public async Task Three_party_transfer_moves_the_contract_deposit_meters_and_roommates_to_the_new_holder()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var sessions = new Sessions(client);
        var oldId = await ArrangeOldContractAsync(factory, sessions, closeLastMonth: true);
        await SetMaxOccupantsAsync(factory, 3);
        await sessions.As("customer@example.test");
        var staying = await AddRoommateAsync(client, oldId, "Customer B", "079201001111");
        await AddRoommateAsync(client, oldId, "Lê Văn C", "079201002222");

        using var created = await client.PostAsJsonAsync(Url, new
        {
            contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth, occupantId = staying
        });
        var transferId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractTransferId").GetInt32();
        await sessions.As("manager@example.test");
        var beforeNewHolder = (await client.GetFromJsonAsync<JsonElement>(Url))[0];
        await sessions.As("other@example.test");
        var newHolderView = (await client.GetFromJsonAsync<JsonElement>(Url))[0];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Url}/{transferId}/confirm", null)).StatusCode);
        await sessions.As("manager@example.test");
        using var completed = await client.PostAsync($"{Url}/{transferId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(["Người nhận chưa xác nhận."], beforeNewHolder.GetProperty("blockers").EnumerateArray().Select(item => item.GetString()));
        Assert.False(beforeNewHolder.GetProperty("canComplete").GetBoolean());
        Assert.True(newHolderView.GetProperty("canConfirm").GetBoolean());
        Assert.Empty(newHolderView.GetProperty("blockers").EnumerateArray());
        Assert.Equal(3_000_000m, newHolderView.GetProperty("depositCarriedOver").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var newId = (await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("newContractId").GetInt32();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = await db.Contracts.SingleAsync(contract => contract.ContractId == oldId);
        var next = await db.Contracts.SingleAsync(contract => contract.ContractId == newId);
        Assert.Equal(("Terminated", ThisMonth.AddDays(-1)), (old.Status, old.EndDate));
        Assert.Equal((20, "Active", ThisMonth, Today.AddYears(1), 3_000_000m, 3_000_000m, oldId),
            (next.CustomerId, next.Status, next.StartDate, next.EndDate, next.MonthlyRent, next.DepositAmount, next.TransferredFromContractId!.Value));
        Assert.Equal((150m, 14m, ThisMonth), (next.InitialElectricReading!.Value, next.InitialWaterReading!.Value, next.MeterHandoverDate!.Value));
        Assert.True(await db.HandoverRecords.AnyAsync(record => record.ContractId == newId && record.Kind == "MoveIn"));
        Assert.Equal(["Lê Văn C"], await db.ContractOccupants.Where(occupant => occupant.ContractId == newId).Select(occupant => occupant.FullName).ToListAsync());
        Assert.Equal(2, await db.ContractOccupants.CountAsync(occupant => occupant.ContractId == oldId && occupant.MoveOutDate != null));
        Assert.Equal("Occupied", (await db.Rooms.SingleAsync(room => room.RoomId == 1)).Status);

        await sessions.As("other@example.test");
        var nextDto = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{newId}");
        Assert.Equal((3_000_000m, oldId), (nextDto.GetProperty("securityDepositAmount").GetDecimal(), nextDto.GetProperty("transferredFromContractId").GetInt32()));
        await sessions.As("manager@example.test");
        var oldDto = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{oldId}");
        Assert.Equal((0m, newId), (oldDto.GetProperty("securityDepositAmount").GetDecimal(), oldDto.GetProperty("transferredToContractId").GetInt32()));
    }

    [Fact]
    public async Task Completion_lists_and_enforces_every_missing_step()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var sessions = new Sessions(client);
        var oldId = await ArrangeOldContractAsync(factory, sessions, closeLastMonth: false);

        using var created = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth });
        var transfer = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transferId = transfer.GetProperty("contractTransferId").GetInt32();
        using var complete = await client.PostAsync($"{Url}/{transferId}/complete", null);

        var blockers = transfer.GetProperty("blockers").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("Người đứng tên cũ chưa xác nhận.", blockers);
        Assert.Contains("Người nhận chưa xác nhận.", blockers);
        Assert.Contains(blockers, item => item.StartsWith("Cần lập hóa đơn tháng", StringComparison.Ordinal));
        Assert.Contains("Cần lập biên bản kiểm tra thiết bị (biên bản trả phòng của hợp đồng cũ).", blockers);
        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Contracts.CountAsync());
    }

    [Fact]
    public async Task Damaged_equipment_blocks_carrying_the_deposit_over()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var sessions = new Sessions(client);
        await AddEquipmentAsync(factory);
        var oldId = await ArrangeOldContractAsync(factory, sessions, closeLastMonth: true, moveOutCondition: "Damaged");

        var transfer = await (await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(transfer.GetProperty("blockers").EnumerateArray().Select(item => item.GetString()!),
            item => item.StartsWith("Biên bản có thiết bị hư hỏng hoặc mất", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Requests_are_validated_and_only_the_parties_can_act()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var sessions = new Sessions(client);
        var oldId = await ArrangeOldContractAsync(factory, sessions, closeLastMonth: true);

        var notFirstDay = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth.AddDays(1) });
        var unknownEmail = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "nobody@example.test", effectiveDate = ThisMonth });
        var sameHolder = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "customer@example.test", effectiveDate = ThisMonth });
        using var created = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth });
        var duplicate = await client.PostAsJsonAsync(Url, new { contractId = oldId, newHolderEmail = "other@example.test", effectiveDate = ThisMonth });
        var transferId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractTransferId").GetInt32();
        await sessions.As("manager-b@example.test");
        var otherManagerCancels = await client.PostAsJsonAsync($"{Url}/{transferId}/cancel", new { reason = "x" });
        await sessions.As("admin@example.test");
        var adminConfirms = await client.PostAsync($"{Url}/{transferId}/confirm", null);
        await sessions.As("other@example.test");
        using var cancelled = await client.PostAsJsonAsync($"{Url}/{transferId}/cancel", new { reason = "Đổi ý, không ở lại" });

        Assert.Equal("Ngày hiệu lực phải là ngày 1 của một tháng (điện nước và hóa đơn chốt theo tháng).",
            (await notFirstDay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("effectiveDate")[0].GetString());
        Assert.Contains("Chưa có tài khoản khách", (await unknownEmail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, sameHolder.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherManagerCancels.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminConfirms.StatusCode);
        var body = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Cancelled", "Đổi ý, không ở lại"), (body.GetProperty("status").GetString(), body.GetProperty("cancelReason").GetString()));
    }

    // Contract on room A101 that started last month (deposit 3.000.000đ held as security). With closeLastMonth the
    // manager has recorded last month's meters (100→150 kWh, 10→14 m³), issued and collected its invoice and
    // recorded the move-out inspection. Ends logged in as Manager A.
    private static async Task<int> ArrangeOldContractAsync(BookingsApiFactory factory, Sessions sessions, bool closeLastMonth,
        string moveOutCondition = "Good")
    {
        var client = sessions.Client;
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        sessions.Remember("customer@example.test");
        await sessions.As("manager@example.test");
        using var response = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        var contractId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contract = await db.Contracts.SingleAsync(item => item.ContractId == contractId);
            contract.StartDate = LastMonth;
            contract.InitialElectricReading = 100m;
            contract.InitialWaterReading = 10m;
            contract.MeterHandoverDate = LastMonth;
            await db.SaveChangesAsync();
        }
        if (!closeLastMonth)
        {
            return contractId;
        }

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId, readingMonth = LastMonth, electricEnd = 150m, waterEnd = 14m
        })).StatusCode);
        using var invoice = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = LastMonth, dueDate = Today.AddDays(10) });
        Assert.Equal(HttpStatusCode.Created, invoice.StatusCode);
        var invoiceId = (await invoice.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null)).StatusCode);
        var equipment = await EquipmentIdsAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/contracts/{contractId}/handovers/move-out", new
        {
            handoverDate = Today,
            items = equipment.Select(id => new { equipmentId = id, condition = moveOutCondition })
        })).StatusCode);
        return contractId;
    }

    private static async Task<int> AddRoommateAsync(HttpClient client, int contractId, string name, string cccd)
    {
        using var response = await client.PostAsJsonAsync($"/api/contracts/{contractId}/occupants",
            new { fullName = name, phoneNumber = "0905123456", identityNumber = cccd, moveInDate = Today });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("occupants")
            .EnumerateArray().Last().GetProperty("contractOccupantId").GetInt32();
    }

    private static async Task SetMaxOccupantsAsync(BookingsApiFactory factory, int max)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Rooms.SingleAsync(room => room.RoomId == 1)).MaxOccupants = max;
        await db.SaveChangesAsync();
    }

    private static async Task AddEquipmentAsync(BookingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Equipments.Add(new Equipments
        {
            EquipmentId = 701, RoomId = 1, Name = "Máy lạnh", Quantity = 1, CompensationValue = 8_500_000m,
            InstallDate = new DateOnly(2025, 1, 1), WarrantyExpiry = new DateOnly(2027, 1, 1),
            Supplier = "Điện máy Xanh", Status = "Operational"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<List<int>> EquipmentIdsAsync(BookingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Equipments
            .Where(equipment => equipment.RoomId == 1).Select(equipment => equipment.EquipmentId).ToListAsync();
    }

    // Đăng nhập bị giới hạn 5 lần/phút (chống dò mật khẩu): mỗi tài khoản chỉ đăng nhập một lần, sau đó dùng lại token.
    private sealed class Sessions(HttpClient client)
    {
        private readonly Dictionary<string, AuthenticationHeaderValue> _tokens = new();
        public HttpClient Client => client;

        public void Remember(string email) => _tokens[email] = client.DefaultRequestHeaders.Authorization!;

        public async Task As(string email)
        {
            if (_tokens.TryGetValue(email, out var token))
            {
                client.DefaultRequestHeaders.Authorization = token;
                return;
            }
            await LoginAsync(client, email);
            Remember(email);
        }
    }
}
