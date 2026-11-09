using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class ReceivablesApiTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_management_can_read_report(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        if (email is not null) await LoginAsync(client, email);
        Assert.Equal(expected, (await client.GetAsync("/api/reports/receivables")).StatusCode);
    }

    [Theory]
    [InlineData("admin@example.test", null, 1050, 4, 550, 2)]
    [InlineData("admin@example.test", 1, 650, 3, 150, 1)]
    [InlineData("manager@example.test", null, 650, 3, 150, 1)]
    [InlineData("manager@example.test", 1, 650, 3, 150, 1)]
    [InlineData("manager-b@example.test", null, 400, 1, 400, 1)]
    public async Task Totals_use_invoice_lines_due_dates_and_owner_scope(
        string email, int? propertyId, int total, int count, int overdue, int overdueCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await LoginAsync(client, email);
        var url = "/api/reports/receivables" + (propertyId.HasValue ? $"?propertyId={propertyId}" : "");
        var report = (await client.GetFromJsonAsync<ReceivablesReportDto>(url))!;
        Assert.Equal(propertyId, report.PropertyId);
        Assert.Equal(Today(), report.AsOfDate);
        Assert.Equal((decimal)total, report.OutstandingAmount);
        Assert.Equal(count, report.OutstandingInvoiceCount);
        Assert.Equal((decimal)overdue, report.OverdueAmount);
        Assert.Equal(overdueCount, report.OverdueInvoiceCount);
        Assert.Equal(report.OutstandingAmount, report.OverdueAmount + report.NotYetDueAmount);
        Assert.Equal(report.OutstandingInvoiceCount, report.OverdueInvoiceCount + report.NotYetDueInvoiceCount);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(6, await context.Invoices.CountAsync());
        Assert.Equal("Unpaid", (await context.Invoices.SingleAsync(invoice => invoice.InvoiceId == 1)).Status);
    }

    [Fact]
    public async Task Owner_cannot_override_scope_and_empty_report_returns_zeros()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/reports/receivables?propertyId=2&ownerId=8")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/reports/receivables?propertyId=999")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Properties.Add(new Property { PropertyId = 3, OwnerId = 7, PropertyName = "Empty" });
            await context.SaveChangesAsync();
        }
        var empty = (await client.GetFromJsonAsync<ReceivablesReportDto>("/api/reports/receivables?propertyId=3"))!;
        Assert.Equal(0m, empty.OutstandingAmount);
        Assert.Equal(0, empty.OutstandingInvoiceCount);
        Assert.Equal(0m, empty.NotYetDueAmount);
        Assert.Equal(0m, empty.OverdueAmount);
    }

    [Theory]
    [InlineData("propertyId=0")]
    [InlineData("propertyId=-1")]
    [InlineData("propertyId=abc")]
    [InlineData("asOfDate=2020-01-01")]
    [InlineData("asOfDate=2099-01-01")]
    [InlineData("asOfDate=invalid")]
    public async Task Invalid_or_historical_filters_return_bad_request(string query)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/reports/receivables?" + query)).StatusCode);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    private static async Task SeedAsync(BookingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = Today();
        context.Properties.Add(new Property { PropertyId = 2, OwnerId = 8, PropertyName = "Other" });
        context.Rooms.Add(new Rooms { RoomId = 3, PropertyId = 2, RoomTypeId = 1, RoomCode = "B101", Status = "Occupied" });
        context.Contracts.AddRange(
            new Contracts { ContractId = 1, RoomId = 1, CustomerId = 10, ProcessedByStaffId = 7,
                StartDate = today.AddMonths(-6), EndDate = today.AddMonths(-1), Status = "Active" },
            new Contracts { ContractId = 2, RoomId = 3, CustomerId = 20, ProcessedByStaffId = 8,
                StartDate = today.AddMonths(-6), EndDate = today.AddMonths(6), Status = "Active" });
        Invoices Invoice(int id, int contract, string status, DateOnly due, params decimal[] amounts) => new()
        {
            InvoiceId = id, ContractId = contract, Status = status, DueDate = due,
            InvoiceMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-id),
            InvoiceDetails = amounts.Select(amount => new InvoiceDetails { Amount = amount, ItemType = "Rent", Description = "Test line" }).ToList()
        };
        context.Invoices.AddRange(
            Invoice(1, 1, "Unpaid", today.AddDays(-1), 100.25m, 49.75m),
            Invoice(2, 1, "Unpaid", today, 200m),
            Invoice(3, 1, "Overdue", today.AddDays(1), 300m),
            Invoice(4, 1, "Paid", today.AddDays(-10), 900m),
            Invoice(5, 2, "Unpaid", today.AddDays(-3), 400m),
            Invoice(6, 1, "Unpaid", today.AddDays(-3), 0m));
        // Booking deposits must not contribute to invoice debt.
        context.Bookings.Add(new Bookings { BookingId = 1, RoomId = 1, CustomerId = 10,
            Status = "Deposited", DepositAmount = 5000000, ExpiryDate = DateTime.UtcNow.AddDays(7) });
        await context.SaveChangesAsync();
    }
}
