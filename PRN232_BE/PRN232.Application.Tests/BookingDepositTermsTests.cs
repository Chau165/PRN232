using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Migrations;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.3 (PLAN.md D3 bước 4): under the new terms the deposit is a security deposit held until
// move-out; the first invoice bills the full rent. Bookings made under the old terms keep their credit.
public sealed class BookingDepositTermsTests
{
    private static readonly DateOnly VietnamToday =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Fact]
    public async Task New_bookings_use_the_first_month_deposit_rule_by_default()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(client);

        using var scope = factory.Services.CreateScope();
        var booking = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings
            .SingleAsync(item => item.BookingId == bookingId);
        Assert.True(booking.DepositAppliesToFirstMonth);
    }

    [Fact]
    public async Task New_terms_bill_the_full_first_month_and_hold_the_deposit_on_the_contract()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = false };
        using var client = factory.CreateClient();
        var contractId = await InvoicesApiTests.CreateContractWithReadingAsync(client);

        using var preview = await client.PostAsJsonAsync("/api/invoices/preview", new
        {
            contractId,
            invoiceMonth = new DateOnly(VietnamToday.Year, VietnamToday.Month, 1),
            dueDate = VietnamToday.AddDays(10)
        });

        // 3.000.000 rent + 355.000 electricity, water and internet; nothing is deducted.
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var invoice = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((3_355_000m, 0m), (invoice.GetProperty("total").GetDecimal(), invoice.GetProperty("prepaidRentApplied").GetDecimal()));
        Assert.DoesNotContain(invoice.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("itemType").GetString() == "PrepaidRentCredit");

        var contract = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{contractId}");
        Assert.Equal((3_000_000m, 0m, 0m), (contract.GetProperty("securityDepositAmount").GetDecimal(),
            contract.GetProperty("prepaidRentAmount").GetDecimal(), contract.GetProperty("remainingPrepaidCredit").GetDecimal()));
        using var scope = factory.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.SingleAsync()).DepositAppliesToFirstMonth);
    }

    [Fact]
    public async Task Bookings_made_under_the_old_terms_keep_the_first_month_credit()
    {
        using var factory = new BookingsApiFactory { LegacyDepositTerms = true };
        using var client = factory.CreateClient();
        var contractId = await InvoicesApiTests.CreateContractWithReadingAsync(client);

        var contract = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/{contractId}");

        Assert.Equal((0m, 3_000_000m), (contract.GetProperty("securityDepositAmount").GetDecimal(),
            contract.GetProperty("prepaidRentAmount").GetDecimal()));
    }

    [Fact]
    public void Migration_keeps_every_existing_booking_on_the_old_terms()
    {
        var migration = new AddBookingDepositTerms();
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal(("Bookings", "DepositAppliesToFirstMonth", true), (column.Table, column.Name, column.DefaultValue));
    }
}
