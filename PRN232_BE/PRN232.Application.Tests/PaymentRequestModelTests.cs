using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class PaymentRequestModelTests
{
    [Theory]
    [InlineData("Creating", true)]
    [InlineData("Pending", true)]
    [InlineData("Succeeded", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Expired", true)]
    [InlineData("Create-failed", true)]
    [InlineData("RequiresReview", true)]
    [InlineData("Failed", false)]
    [InlineData("CreateFailed", false)]
    [InlineData(null, false)]
    public void Request_statuses_are_distinct_from_settlement_status(string? status, bool expected)
    {
        Assert.Equal(expected, PaymentRequestStatuses.IsValid(status));
    }

    [Fact]
    public async Task Creating_request_round_trips_without_creating_a_settlement()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paymentCount = await context.Payments.CountAsync();
        var now = new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc);
        var request = new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = 10, Amount = 3355000m, OrderCode = 123456789L,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        context.PaymentRequests.Add(request);
        await context.SaveChangesAsync();
        var requestId = request.PaymentRequestId;
        context.ChangeTracker.Clear();
        var stored = await context.PaymentRequests.Include(item => item.Invoice).Include(item => item.Customer)
            .SingleAsync(item => item.PaymentRequestId == requestId);
        Assert.Equal(PaymentRequestStatuses.Creating, stored.Status);
        Assert.Equal("VND", stored.Currency);
        Assert.Equal("PayOS", stored.Provider);
        Assert.Equal(123456789L, stored.OrderCode);
        Assert.Equal(3355000m, stored.Amount);
        Assert.Equal(now, stored.CreatedAtUtc);
        Assert.Null(stored.PaymentId);
        Assert.Null(stored.PaymentLinkId);
        Assert.Null(stored.ExpiresAtUtc);
        Assert.Null(stored.ConfirmedAtUtc);
        Assert.Equal(invoiceId, stored.Invoice!.InvoiceId);
        Assert.Equal("Unpaid", stored.Invoice.Status);
        Assert.Equal(10, stored.Customer!.UserId);
        Assert.Equal(paymentCount, await context.Payments.CountAsync());
        // Creation may fail without money having been received.
        stored.Status = PaymentRequestStatuses.CreateFailed;
        stored.LastErrorCode = "INVALID_REQUEST";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal("Create-failed", (await context.PaymentRequests.SingleAsync()).Status);
    }
}
