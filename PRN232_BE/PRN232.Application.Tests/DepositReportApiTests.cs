using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
namespace PRN232.Application.Tests;

public class DepositReportApiTests
{
    private const string Url = "/api/reports/booking-deposits?fromMonth=2026-11-01&toMonth=2026-11-01";
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Financial_report_requires_management(string? email, HttpStatusCode status)
    {
        using var f = new BookingsApiFactory(); using var c = f.CreateClient();
        if (email != null) await LoginAsync(c, email);
        Assert.Equal(status, (await c.GetAsync(Url)).StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_refund_is_counted_once_and_unknown_bank_time_is_not_invented(bool unknown)
    {
        using var f = new BookingsApiFactory(); using var c = f.CreateClient();
        var id = await BookingRefundPayoutTests.ApproveAsync(f, c);
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = await db.Payments.SingleAsync(); p.PaidAtUtc = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc);
            // Failed receipts never contribute. Approved requests are not outgoing money.
            db.Payments.Add(new Payments { BookingId = p.BookingId, UserId = 10, Amount = 999, PaymentMethod = "PayOS", Status = "Failed", PaidAtUtc = p.PaidAtUtc });
            await db.SaveChangesAsync();
        }
        var before = (await c.GetFromJsonAsync<DepositReportDto>(Url))!;
        Assert.Equal(3000000m, before.Totals.CollectedAmount); Assert.Equal(0m, before.Totals.RefundedAmount);
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            // Remove the failed fixture before business finalization validation.
            db.Payments.RemoveRange(await db.Payments.Where(p => p.Status == "Failed").ToListAsync()); await db.SaveChangesAsync();
            var a = (await s.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>().TryClaimAsync(id, DateTime.UtcNow, default))!;
            a.Status = "Succeeded"; a.ProviderPayoutId = "report-payout"; a.ProviderTransactionId = "report-transaction";
            a.ProviderApprovalState = "Completed"; a.ProviderTransactionState = "Succeeded";
            a.SubmittedAtUtc = a.CreatedAtUtc; a.CompletedAtUtc = DateTime.UtcNow; a.UpdatedAtUtc = a.CompletedAtUtc.Value;
            await db.SaveChangesAsync();
            var completion = s.ServiceProvider.GetRequiredService<IBookingRefundCompletionRepository>();
            await completion.CompleteAsync(a.PayoutAttemptId, DateTime.UtcNow, default);
            await completion.CompleteAsync(a.PayoutAttemptId, DateTime.UtcNow, default);
            var refund = await db.BookingRefunds.SingleAsync();
            refund.TransferredAtUtc = unknown ? null : new DateTime(2026, 11, 30, 16, 59, 59, DateTimeKind.Utc);
            refund.RecordedAtUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }
        var response = await c.GetAsync(Url); Assert.True(response.Headers.CacheControl!.NoStore);
        var report = (await response.Content.ReadFromJsonAsync<DepositReportDto>())!;
        Assert.Equal(1, report.Totals.CollectionCount);
        Assert.Equal(unknown ? 0m : 2500000m, report.Totals.RefundedAmount);
        Assert.Equal(unknown ? 2500000m : 0m, report.UnknownDate.RefundedAmount);
        Assert.Equal(1, report.Totals.RefundCount + report.UnknownDate.RefundCount);
        Assert.Equal(report.Totals.NetCashFlow, report.Monthly.Sum(x => x.Totals.NetCashFlow));
        Assert.Equal(report.Totals.CollectedAmount, report.ByProperty.Sum(x => x.Totals.CollectedAmount));
        var december = (await c.GetFromJsonAsync<DepositReportDto>(Url.Replace("2026-11-01", "2026-12-01")))!;
        Assert.Equal(0m, december.Totals.CollectedAmount); Assert.Equal(0m, december.Totals.RefundedAmount);
        Assert.Equal(report.UnknownDate, december.UnknownDate);
    }
    [Fact]
    public async Task Legacy_unknown_receipts_are_visible_and_current_manager_assignment_controls_history()
    {
        using var f = new BookingsApiFactory(); using var c = f.CreateClient();
        await BookingRefundRequestApiTests.SeedAsync(f);
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.Payments.SingleAsync()).PaidAtUtc = null;
            db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.ToListAsync());
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 }); await db.SaveChangesAsync();
        }
        await LoginAsync(c, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Url + "&propertyId=1")).StatusCode);
        Assert.Empty((await c.GetFromJsonAsync<DepositReportDto>(Url))!.ByProperty);
        await LoginAsync(c, "manager-b@example.test");
        var report = (await c.GetFromJsonAsync<DepositReportDto>(Url + "&propertyId=1"))!;
        Assert.Equal(3000000m, report.UnknownDate.CollectedAmount); Assert.Equal(1, report.UnknownDate.CollectionCount);
        Assert.Equal(0m, report.Totals.CollectedAmount);
    }
    [Theory]
    [InlineData("fromMonth=2026-11-02&toMonth=2026-11-01")]
    [InlineData("fromMonth=2027-01-01&toMonth=2026-11-01")]
    [InlineData("fromMonth=2024-01-01&toMonth=2026-11-01")]
    [InlineData("propertyId=0")]
    public async Task Rejects_invalid_filters(string query)
    {
        using var f = new BookingsApiFactory(); using var c = f.CreateClient(); await LoginAsync(c, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/reports/booking-deposits?" + query)).StatusCode);
    }
}
