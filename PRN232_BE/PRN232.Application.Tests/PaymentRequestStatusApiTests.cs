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

public class PaymentRequestStatusApiTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer@example.test", HttpStatusCode.OK)]
    [InlineData("other@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    public async Task Read_access_follows_customer_and_current_property_owner(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = null;
        if (email is not null) await LoginAsync(client, email);
        var response = await client.GetAsync($"/api/payment-requests/{id}?customerId=10&ownerId=7");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.True(response.Headers.CacheControl!.NoStore);
            var body = (await response.Content.ReadFromJsonAsync<PaymentRequestStatusDto>())!;
            Assert.Equal(id, body.PaymentRequestId);
            Assert.Equal(10, body.CustomerId);
            Assert.Equal(1, body.PropertyId);
            Assert.Equal(3355000m, body.Amount);
            Assert.Equal("Unpaid", body.InvoiceStatus);
            Assert.True(body.CanOpenCheckout);
            Assert.False(body.IsExpired);
            Assert.NotNull(body.CheckoutUrl);
        }
    }

    [Theory]
    [InlineData("Creating")]
    [InlineData("Pending")]
    [InlineData("Succeeded")]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    [InlineData("Create-failed")]
    [InlineData("RequiresReview")]
    public async Task Stored_status_is_returned_without_mutation_or_internal_provider_details(string status)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory, client, status);
        await LoginAsync(client, "customer@example.test");
        var response = await client.GetAsync($"/api/payment-requests/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internal-secret", json);
        Assert.DoesNotContain("lastError", json);
        Assert.DoesNotContain("orderCode", json);
        Assert.DoesNotContain("providerReference", json);
        var body = (await response.Content.ReadFromJsonAsync<PaymentRequestStatusDto>())!;
        Assert.Equal(status, body.Status);
        Assert.Equal(status == "Pending", body.CanOpenCheckout);
        Assert.Equal(status == "Pending" ? "https://pay.payos.vn/web/test" : null, body.CheckoutUrl);
        if (status == "Succeeded")
        {
            Assert.Equal("Paid", body.InvoiceStatus);
            Assert.NotNull(body.PaymentId);
            Assert.NotNull(body.PaidAtUtc);
            Assert.NotNull(body.ConfirmedAtUtc);
        }
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await context.PaymentRequests.SingleAsync();
        Assert.Equal(status, stored.Status);
        Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        Assert.Equal(status == "Succeeded" ? 1 : 0, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    [Fact]
    public async Task Elapsed_local_deadline_hides_link_without_marking_request_expired()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory, client, expired: true);
        await LoginAsync(client, "customer@example.test");
        var body = (await client.GetFromJsonAsync<PaymentRequestStatusDto>($"/api/payment-requests/{id}"))!;
        Assert.Equal("Pending", body.Status);
        Assert.True(body.IsExpired);
        Assert.False(body.CanOpenCheckout);
        Assert.Null(body.CheckoutUrl);
        using var scope = factory.Services.CreateScope();
        Assert.Equal("Pending", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Transferring_property_changes_manager_access_to_request_history()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var id = await SeedAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.Properties.SingleAsync()).OwnerId = 8;
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/payment-requests/{id}")).StatusCode);
        await LoginAsync(client, "manager-b@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payment-requests/{id}")).StatusCode);
    }

    [Theory]
    [InlineData(999, HttpStatusCode.NotFound)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    public async Task Missing_and_invalid_identifiers_have_explicit_responses(int id, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(expected, (await client.GetAsync($"/api/payment-requests/{id}")).StatusCode);
    }

    private static async Task<int> SeedAsync(BookingsApiFactory factory, HttpClient client, string status = "Pending", bool expired = false)
    {
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow.AddHours(-1);
        var request = new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = 10, Amount = 3355000m, OrderCode = 123,
            Status = status, CreatedAtUtc = now, UpdatedAtUtc = now,
            ExpiresAtUtc = expired ? now.AddMinutes(30) : now.AddHours(2),
            CheckoutUrl = "https://pay.payos.vn/web/test", PaymentLinkId = "test-link",
            LastErrorMessage = "internal-secret", LastErrorCode = "internal-secret", ProviderReference = "internal-secret"
        };
        if (status == "Succeeded")
        {
            request.Payment = new Payments { InvoiceId = invoiceId, UserId = 10, Amount = request.Amount,
                PaymentMethod = "PayOS", Status = "Success", PaidAtUtc = now };
            request.ConfirmedAtUtc = now;
            (await context.Invoices.SingleAsync()).Status = "Paid";
        }
        context.PaymentRequests.Add(request);
        await context.SaveChangesAsync();
        return request.PaymentRequestId;
    }
}
