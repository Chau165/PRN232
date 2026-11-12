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

public class RevenueApiTests
{
    private const string Url = "/api/reports/revenue?fromMonth=2026-11-01&toMonth=2026-11-01";

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_management_can_view_revenue(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        if (email is not null) await LoginAsync(client, email);
        Assert.Equal(expected, (await client.GetAsync(Url)).StatusCode);
    }

    [Theory]
    [InlineData("admin@example.test", null, 801, 3, 120, 2)]
    [InlineData("admin@example.test", 1, 301, 2, 50, 1)]
    [InlineData("manager@example.test", null, 301, 2, 50, 1)]
    [InlineData("manager-b@example.test", null, 500, 1, 70, 1)]
    public async Task Revenue_uses_payment_dates_vietnam_boundaries_and_excludes_deposits_and_failed_payments(
        string email, int? propertyId, int total, int count, int unknown, int unknownCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await LoginAsync(client, email);
        var report = (await client.GetFromJsonAsync<RevenueReportDto>(Url + (propertyId.HasValue ? $"&propertyId={propertyId}" : "")))!;
        Assert.Equal((decimal)total, report.TotalCollected);
        Assert.Equal(count, report.PaymentCount);
        Assert.Equal((decimal)unknown, report.UnknownDateAmount);
        Assert.Equal(unknownCount, report.UnknownDatePaymentCount);
        Assert.Equal(report.TotalCollected, report.Monthly.Sum(row => row.CollectedAmount));
        Assert.Equal(report.TotalCollected, report.ByProperty.Sum(row => row.CollectedAmount));
        Assert.Equal(report.TotalCollected, report.MonthlyByProperty.Sum(row => row.CollectedAmount));
        Assert.Equal(report.PaymentCount, report.Monthly.Sum(row => row.PaymentCount));
        Assert.Equal(report.UnknownDateAmount, report.ByProperty.Sum(row => row.UnknownDateAmount));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(9, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.CountAsync());
    }

    [Fact]
    public async Task Current_assigned_manager_sees_history_and_old_manager_loses_access()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.PropertyManagerAssignments.RemoveRange(await context.PropertyManagerAssignments.Where(item => item.PropertyId == 1).ToListAsync());
            context.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url + "&propertyId=1&ownerId=8")).StatusCode);
        var oldOwner = (await client.GetFromJsonAsync<RevenueReportDto>(Url))!;
        Assert.Equal(0m, oldOwner.TotalCollected);
        Assert.DoesNotContain(oldOwner.ByProperty, property => property.PropertyId == 1);
        Assert.Equal(0m, oldOwner.UnknownDateAmount);
        await LoginAsync(client, "manager-b@example.test");
        var newOwner = (await client.GetFromJsonAsync<RevenueReportDto>(Url + "&propertyId=1"))!;
        Assert.Equal(301m, newOwner.TotalCollected);
        Assert.Equal(50m, newOwner.UnknownDateAmount);
    }

    [Fact]
    public async Task Empty_months_properties_and_unknown_dates_are_reported_separately()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await LoginAsync(client, "admin@example.test");
        var report = (await client.GetFromJsonAsync<RevenueReportDto>(
            "/api/reports/revenue?fromMonth=2027-01-01&toMonth=2027-03-01"))!;
        Assert.Equal(3, report.Monthly.Count);
        Assert.All(report.Monthly, month => Assert.Equal(0m, month.CollectedAmount));
        Assert.Equal(120m, report.UnknownDateAmount);
        Assert.Equal(9, report.MonthlyByProperty.Count);
        var empty = Assert.Single(report.ByProperty, property => property.PropertyId == 3);
        Assert.Equal(0m, empty.CollectedAmount);
        Assert.Equal(0, empty.UnknownDatePaymentCount);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url + "&propertyId=999")).StatusCode);
    }

    [Theory]
    [InlineData("fromMonth=2026-11-02&toMonth=2026-12-01")]
    [InlineData("fromMonth=2026-12-01&toMonth=2026-11-01")]
    [InlineData("fromMonth=2024-01-01&toMonth=2026-01-01")]
    [InlineData("fromMonth=invalid")]
    [InlineData("propertyId=0")]
    [InlineData("propertyId=abc")]
    [InlineData("fromMonth=9999-12-01&toMonth=9999-12-01")]
    public async Task Invalid_filters_return_bad_request(string query)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/revenue?" + query)).StatusCode);
    }

    private static async Task SeedAsync(BookingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Properties.AddRange(new Property { PropertyId = 2, OwnerId = 8, PropertyName = "Other" },
            new Property { PropertyId = 3, OwnerId = 7, PropertyName = "Empty" });
        context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 },
            new PropertyManagerAssignment { PropertyId = 3, ManagerId = 7 });
        context.Rooms.Add(new Rooms { RoomId = 3, PropertyId = 2, RoomTypeId = 1, RoomCode = "B101", Status = "Occupied" });
        foreach (var id in new[] { 1, 2 })
        {
            context.Contracts.Add(new Contracts { ContractId = id, RoomId = id == 1 ? 1 : 3, CustomerId = 10,
                ProcessedByStaffId = 7, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), Status = "Active" });
            context.Invoices.Add(new Invoices { InvoiceId = id, ContractId = id, InvoiceMonth = new DateOnly(2026, 10, 1),
                DueDate = new DateOnly(2026, 11, 5), Status = "Paid", InvoiceDetails = new List<InvoiceDetails>
                { new() { ItemType = "Rent", Amount = 900, Description = "Room" }, new() { ItemType = "Service", Amount = 100, Description = "Internet" } } });
        }
        context.Bookings.Add(new Bookings { BookingId = 1, RoomId = 1, CustomerId = 10, Status = "Deposited", ExpiryDate = DateTime.UtcNow });
        Payments Payment(int id, int? invoice, decimal amount, DateTime? when, string status = "Success") => new()
        {
            PaymentId = id, InvoiceId = invoice, BookingId = invoice is null ? 1 : null, UserId = 10,
            Amount = amount, PaidAtUtc = when, Status = status, PaymentMethod = id % 2 == 0 ? "Cash" : "MoMo"
        };
        DateTime Utc(int month, int day, int hour, int minute = 0, int second = 0) => new(2026, month, day, hour, minute, second, DateTimeKind.Utc);
        context.Payments.AddRange(
            Payment(1, 1, 99, Utc(10, 31, 16, 59, 59)),
            Payment(2, 1, 100.25m, Utc(10, 31, 17)),
            Payment(3, 1, 200.75m, Utc(11, 30, 16, 59, 59)),
            Payment(4, 1, 400, Utc(11, 30, 17)),
            Payment(5, 1, 50, null), Payment(6, 1, 999, Utc(11, 1, 0), "Failed"),
            Payment(7, null, 888, Utc(11, 1, 0)), Payment(8, 2, 500, Utc(11, 1, 0)), Payment(9, 2, 70, null));
        await context.SaveChangesAsync();
    }
}
