using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Tiền phòng tháng đầu/tháng cuối không trọn tính theo số ngày ở (RentProration). Hợp đồng A101: 3.000.000đ/tháng.
public sealed class RentProrationApiTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static readonly DateOnly ThisMonth = new(Today.Year, Today.Month, 1);
    private static readonly DateOnly LastMonth = ThisMonth.AddMonths(-1);

    // Luật cọc cũ (cọc trừ vào tiền phòng): tháng đầu chỉ ở nửa tháng nên tiền phòng ít hơn tiền cọc; phần cọc còn lại
    // phải được trừ tiếp vào tháng sau, tổng đã trừ đúng bằng tiền cọc đã đóng.
    [Fact]
    public async Task Legacy_deposit_left_over_after_a_short_first_month_is_credited_on_the_next_invoice()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = true };
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(factory, client, startDate: LastMonth.AddDays(15));
        var lastMonthDays = ThisMonth.AddDays(-1).Day;

        var first = await InvoiceAsync(client, contractId, LastMonth, electricEnd: 150m, waterEnd: 14m);
        var second = await InvoiceAsync(client, contractId, ThisMonth, electricEnd: 200m, waterEnd: 18m);

        var firstRent = Math.Round(3_000_000m * (lastMonthDays - 15) / lastMonthDays, 0, MidpointRounding.AwayFromZero);
        Assert.Equal(firstRent, Rent(first));
        Assert.Equal(firstRent, first.GetProperty("prepaidRentApplied").GetDecimal());
        Assert.Equal(3_000_000m, Rent(second));
        Assert.Equal(3_000_000m - firstRent, second.GetProperty("prepaidRentApplied").GetDecimal());
    }

    // Khách báo trả phòng ngày 10 tháng này: tiền phòng tháng này tính đến hết ngày 10.
    [Fact]
    public async Task The_last_month_is_billed_up_to_the_announced_move_out_date()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(factory, client, startDate: ThisMonth, plannedMoveOut: ThisMonth.AddDays(9));
        var days = ThisMonth.AddMonths(1).AddDays(-1).Day;

        var invoice = await InvoiceAsync(client, contractId, ThisMonth, electricEnd: 150m, waterEnd: 14m);

        var rent = invoice.GetProperty("lines").EnumerateArray().Single(line => line.GetProperty("itemType").GetString() == "Rent");
        Assert.Equal($"Tiền phòng tháng {ThisMonth:MM/yyyy} (10/{days} ngày)", rent.GetProperty("description").GetString());
        Assert.Equal(Math.Round(3_000_000m * 10 / days, 0, MidpointRounding.AwayFromZero), rent.GetProperty("amount").GetDecimal());
    }

    private static decimal Rent(JsonElement invoice) => invoice.GetProperty("lines").EnumerateArray()
        .Single(line => line.GetProperty("itemType").GetString() == "Rent").GetProperty("amount").GetDecimal();

    private static async Task<JsonElement> InvoiceAsync(HttpClient client, int contractId, DateOnly month,
        decimal electricEnd, decimal waterEnd)
    {
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/utility-readings", new
        {
            contractId, readingMonth = month, electricEnd, waterEnd
        })).StatusCode);
        using var response = await client.PostAsJsonAsync("/api/invoices", new { contractId, invoiceMonth = month, dueDate = Today.AddDays(10) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // Hợp đồng tạo qua API (bắt đầu hôm nay) rồi lùi ngày bắt đầu trong DB. Kết thúc khi đang đăng nhập Manager A.
    private static async Task<int> CreateContractAsync(BookingsApiFactory factory, HttpClient client, DateOnly startDate,
        DateOnly? plannedMoveOut = null)
    {
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        using var response = await ContractsApiTests.PostContractAsync(client, bookingId, Today, Today.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contractId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contract = await db.Contracts.SingleAsync(item => item.ContractId == contractId);
        (contract.StartDate, contract.MeterHandoverDate, contract.PlannedMoveOutDate) = (startDate, startDate, plannedMoveOut);
        (contract.InitialElectricReading, contract.InitialWaterReading) = (100m, 10m);
        await db.SaveChangesAsync();
        return contractId;
    }
}
