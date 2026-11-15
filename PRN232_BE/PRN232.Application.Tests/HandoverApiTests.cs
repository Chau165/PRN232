using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 3.5: move-in/move-out handover records. Charges follow BLDS 2015 Điều 479: only damaged or
// missing items, never natural wear, never items already damaged at move-in, capped at the agreed value.
public sealed class HandoverApiTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private const int AirConditioner = 501;
    private const int Desk = 502;

    [Fact]
    public async Task Move_out_charges_follow_the_move_in_record_and_the_agreed_value()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithEquipmentAsync(factory, client);
        var url = $"/api/contracts/{contractId}/handovers";

        using var moveIn = await client.PutAsJsonAsync($"{url}/move-in", Handover(
            (AirConditioner, "Good", null), (Desk, "Damaged", null)));
        Assert.Equal(HttpStatusCode.OK, moveIn.StatusCode);
        // The landlord later revalues the air conditioner; the agreed move-in value still applies.
        await UpdateAsync(factory, db => db.Equipments.Single(e => e.EquipmentId == AirConditioner).CompensationValue = 12_000_000m);

        var alreadyDamaged = await client.PutAsJsonAsync($"{url}/move-out", Handover(
            (AirConditioner, "Good", null), (Desk, "Damaged", 100_000m)));
        var naturalWear = await client.PutAsJsonAsync($"{url}/move-out", Handover(
            (AirConditioner, "Worn", 100_000m), (Desk, "Damaged", null)));
        var overCap = await client.PutAsJsonAsync($"{url}/move-out", Handover(
            (AirConditioner, "Damaged", 9_000_000m), (Desk, "Damaged", null)));
        using var moveOut = await client.PutAsJsonAsync($"{url}/move-out", Handover(
            (AirConditioner, "Damaged", 1_200_000m), (Desk, "Damaged", null)));
        var editMoveInAfterMoveOut = await client.PutAsJsonAsync($"{url}/move-in", Handover(
            (AirConditioner, "Good", null), (Desk, "Good", null)));

        Assert.Equal("Bàn học đã hư hỏng hoặc mất từ lúc nhận phòng nên không tính tiền khách.",
            await FieldErrorAsync(alreadyDamaged, "items[1].chargeAmount"));
        Assert.Contains("hao mòn tự nhiên không phải bồi thường", await FieldErrorAsync(naturalWear, "items[0].chargeAmount"));
        Assert.Equal("Máy lạnh: tiền bồi thường tối đa 8.500.000đ (1 × 8.500.000đ).",
            await FieldErrorAsync(overCap, "items[0].chargeAmount"));
        Assert.Equal(HttpStatusCode.OK, moveOut.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, editMoveInAfterMoveOut.StatusCode);

        var body = await moveOut.Content.ReadFromJsonAsync<JsonElement>();
        var record = body.GetProperty("moveOut");
        Assert.Equal(1_200_000m, record.GetProperty("totalCharge").GetDecimal());
        var airConditioner = record.GetProperty("items")[0];
        Assert.Equal(("Máy lạnh", 8_500_000m, "Good"), (airConditioner.GetProperty("equipmentName").GetString(),
            airConditioner.GetProperty("compensationValue").GetDecimal(), airConditioner.GetProperty("moveInCondition").GetString()));
        Assert.Equal("Manager A", record.GetProperty("recordedByName").GetString());
    }

    [Fact]
    public async Task Only_the_assigned_manager_records_and_only_the_parties_can_read()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithEquipmentAsync(factory, client);
        var url = $"/api/contracts/{contractId}/handovers";
        var body = Handover((AirConditioner, "Good", null), (Desk, "Good", null));

        var incomplete = await client.PutAsJsonAsync($"{url}/move-in", Handover((AirConditioner, "Good", null)));
        var chargedAtMoveIn = await client.PutAsJsonAsync($"{url}/move-in", Handover((AirConditioner, "Good", 1_000m), (Desk, "Good", null)));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{url}/move-in", body)).StatusCode);
        await LoginAsync(client, "customer@example.test");
        var byCustomer = await client.PutAsJsonAsync($"{url}/move-in", body);
        var customerView = await client.GetFromJsonAsync<JsonElement>(url);
        await LoginAsync(client, "other@example.test");
        var otherCustomer = await client.GetAsync(url);
        await LoginAsync(client, "manager-b@example.test");
        var otherManager = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("còn thiếu: Bàn học", (await incomplete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal("Biên bản nhận phòng không tính tiền bồi thường.", await FieldErrorAsync(chargedAtMoveIn, "items[0].chargeAmount"));
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
        Assert.Equal(2, customerView.GetProperty("moveIn").GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, customerView.GetProperty("moveOut").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, otherCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherManager.StatusCode);
    }

    [Fact]
    public async Task Rejects_unknown_conditions_and_future_dates_per_field()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractWithEquipmentAsync(factory, client);

        using var response = await client.PutAsJsonAsync($"/api/contracts/{contractId}/handovers/move-in", new
        {
            handoverDate = Today.AddDays(1),
            items = new[] { new { equipmentId = AirConditioner, condition = "Broken" }, new { equipmentId = Desk, condition = "Good" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Ngày bàn giao không được ở tương lai.", errors.GetProperty("handoverDate")[0].GetString());
        Assert.Equal("Tình trạng phải là Tốt, Hao mòn, Hư hỏng hoặc Mất.", errors.GetProperty("items[0].condition")[0].GetString());
    }

    private static object Handover(params (int EquipmentId, string Condition, decimal? Charge)[] items) => new
    {
        handoverDate = Today,
        note = "Biên bản có chữ ký hai bên",
        items = items.Select(item => new { equipmentId = item.EquipmentId, condition = item.Condition, chargeAmount = item.Charge })
    };

    private static async Task<string?> FieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty(field)[0].GetString();
    }

    private static async Task UpdateAsync(BookingsApiFactory factory, Action<AppDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    // Room A101 gets an air conditioner (1 × 8.500.000đ) and two desks (2 × 500.000đ); ends logged in as Manager A.
    private static async Task<int> CreateContractWithEquipmentAsync(BookingsApiFactory factory, HttpClient client)
    {
        await UpdateAsync(factory, db => db.Equipments.AddRange(
            Equipment(AirConditioner, "Máy lạnh", 1, 8_500_000m),
            Equipment(Desk, "Bàn học", 2, 500_000m)));
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var contract = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, contract.StatusCode);
        return (await contract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }

    private static Equipments Equipment(int id, string name, int quantity, decimal value) => new()
    {
        EquipmentId = id,
        RoomId = 1,
        Name = name,
        Quantity = quantity,
        CompensationValue = value,
        InstallDate = new DateOnly(2025, 1, 1),
        WarrantyExpiry = new DateOnly(2027, 1, 1),
        Supplier = "Điện máy Xanh",
        Status = "Operational"
    };
}
