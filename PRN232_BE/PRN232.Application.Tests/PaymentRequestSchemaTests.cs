using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class PaymentRequestSchemaTests
{
    [Fact]
    public void PostgreSql_migration_snapshot_matches_the_current_model()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static PaymentRequests Request(int invoiceId, long orderCode, string status = "Creating") => new()
    {
        InvoiceId = invoiceId, CustomerId = 10, OrderCode = orderCode, Amount = 1000,
        Status = status, CreatedAtUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)
    };

    [Theory]
    [InlineData("order")]
    [InlineData("link")]
    [InlineData("active")]
    [InlineData("pending")]
    [InlineData("review")]
    [InlineData("payment")]
    public async Task Unique_indexes_reject_duplicate_identifiers_and_active_invoice_requests(string conflict)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = Request(invoiceId, 100, conflict is "order" or "link" or "payment" ? "Create-failed" : "Creating");
        first.PaymentLinkId = "unique-link";
        if (conflict == "payment") first.PaymentId = (await context.Payments.SingleAsync()).PaymentId;
        context.PaymentRequests.Add(first);
        await context.SaveChangesAsync();
        // Use a fresh tracking state so EF relationship fixup cannot move the
        // existing optional one-to-one link before the database checks it.
        context.ChangeTracker.Clear();
        var second = Request(invoiceId, conflict == "order" ? 100 : 101,
            conflict == "pending" ? "Pending" : conflict == "review" ? "RequiresReview" :
            conflict is "order" or "link" or "payment" ? "Create-failed" : "Creating");
        if (conflict == "link") second.PaymentLinkId = "unique-link";
        if (conflict == "payment") second.PaymentId = first.PaymentId;
        context.PaymentRequests.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("order")]
    [InlineData("currency")]
    [InlineData("status")]
    [InlineData("updated")]
    [InlineData("expires")]
    [InlineData("customer")]
    [InlineData("invoice")]
    [InlineData("payment")]
    public async Task Checks_and_foreign_keys_reject_invalid_requests(string invalid)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = Request(invoiceId, 100);
        switch (invalid)
        {
            case "amount": request.Amount = 0; break;
            case "order": request.OrderCode = 0; break;
            case "currency": request.Currency = "USD"; break;
            case "status": request.Status = "Failed"; break;
            case "updated": request.UpdatedAtUtc = request.CreatedAtUtc.AddSeconds(-1); break;
            case "expires": request.ExpiresAtUtc = request.CreatedAtUtc; break;
            case "customer": request.CustomerId = 999; break;
            case "invoice": request.InvoiceId = 999; break;
            case "payment": request.PaymentId = 999; break;
        }
        context.PaymentRequests.Add(request);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Historical_attempts_and_nullable_provider_links_are_allowed_without_touching_payments()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paymentCount = await context.Payments.CountAsync();
        context.PaymentRequests.AddRange(Request(invoiceId, 100, "Create-failed"),
            Request(invoiceId, 101, "Cancelled"), Request(invoiceId, 102, "Expired"), Request(invoiceId, 103));
        await context.SaveChangesAsync();
        Assert.Equal(4, await context.PaymentRequests.CountAsync());
        Assert.Equal(paymentCount, await context.Payments.CountAsync());
        Assert.All(await context.PaymentRequests.ToListAsync(), request => Assert.Null(request.PaymentLinkId));
    }
}
