using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class InvoicePaymentLinkApiTests
{
    private static void Configure(IWebHostBuilder builder, FakeGateway gateway) => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOS:Enabled"] = "true", ["PayOS:ClientId"] = "test-client",
            ["PayOS:ApiKey"] = "test-api-key", ["PayOS:ChecksumKey"] = "test-checksum",
            ["PayOS:ReturnUrl"] = "https://frontend.example.test/my-bills",
            ["PayOS:CancelUrl"] = "https://frontend.example.test/my-bills"
        }))
        .ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvoiceCheckoutGateway>();
            services.AddSingleton<IInvoiceCheckoutGateway>(gateway);
        });

    [Fact]
    public async Task Creates_and_reuses_link_with_server_amount_without_settling_invoice()
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var first = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/payment-link", new { amount = 1, customerId = 20 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var link = (await first.Content.ReadFromJsonAsync<InvoicePaymentLinkDto>())!;
        Assert.Equal(355000m, link.Amount);
        Assert.Equal("Pending", link.Status);
        Assert.False(link.Reused);
        var again = await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var reused = (await again.Content.ReadFromJsonAsync<InvoicePaymentLinkDto>())!;
        Assert.True(reused.Reused);
        Assert.Equal(link.PaymentRequestId, reused.PaymentRequestId);
        Assert.Equal(link.CheckoutUrl, reused.CheckoutUrl);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(355000m, gateway.LastAmount);
        Assert.InRange(gateway.LastExpiry, DateTime.UtcNow.AddMinutes(28), DateTime.UtcNow.AddMinutes(31));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/pay", new { paymentMethod = "MoMo" })).StatusCode);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/invoices/{invoiceId}/record-cash", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
        Assert.Equal(1, await context.PaymentRequests.CountAsync());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("other@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_invoice_customer_can_create_checkout(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        client.DefaultRequestHeaders.Authorization = null;
        if (email is not null) await LoginAsync(client, email);
        Assert.Equal(expected, (await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null)).StatusCode);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Second_click_during_provider_creation_does_not_send_another_create()
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway { Started = new(), Release = new() };
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var first = client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null);
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var second = await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null);
            Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
            Assert.Null((await second.Content.ReadFromJsonAsync<InvoicePaymentLinkDto>())!.CheckoutUrl);
            Assert.Equal(1, gateway.Calls);
        }
        finally { gateway.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
    }

    [Theory]
    [InlineData("timeout", "Creating", HttpStatusCode.Accepted)]
    [InlineData("reject", "Create-failed", HttpStatusCode.BadGateway)]
    [InlineData("mismatch", "RequiresReview", HttpStatusCode.Conflict)]
    public async Task Provider_failures_preserve_safe_request_state(string mode, string state, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway { Mode = mode };
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        var response = await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null);
        Assert.Equal(expected, response.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(state, (await context.PaymentRequests.SingleAsync()).Status);
            Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
        }
        if (mode == "reject")
        {
            gateway.Mode = "ok";
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null)).StatusCode);
            Assert.Equal(2, gateway.Calls);
        }
        else
        {
            var retry = await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null);
            Assert.Equal(expected, retry.StatusCode);
            Assert.Equal(1, gateway.Calls);
        }
    }

    [Theory]
    [InlineData("CANCELLED", HttpStatusCode.Created, 2)]
    [InlineData("EXPIRED", HttpStatusCode.Created, 2)]
    [InlineData("PAID", HttpStatusCode.Conflict, 1)]
    [InlineData("PENDING", HttpStatusCode.Conflict, 1)]
    public async Task Expired_local_link_is_replaced_only_when_provider_confirms_closed_and_unpaid(
        string remoteState, HttpStatusCode expected, int calls)
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway { RemoteState = remoteState };
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await context.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = DateTime.UtcNow.AddHours(-2);
            request.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
            await context.SaveChangesAsync();
        }
        Assert.Equal(expected, (await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null)).StatusCode);
        Assert.Equal(calls, gateway.Calls);
        using var verifyScope = app.Services.CreateScope();
        var oldRequest = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentRequests
            .OrderBy(request => request.PaymentRequestId).FirstAsync();
        Assert.Equal(remoteState == "CANCELLED" ? "Cancelled" : remoteState == "EXPIRED" ? "Expired" : "RequiresReview", oldRequest.Status);
    }

    [Theory]
    [InlineData("paid", HttpStatusCode.Conflict)]
    [InlineData("zero", HttpStatusCode.BadRequest)]
    [InlineData("fractional", HttpStatusCode.BadRequest)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    public async Task Invalid_invoice_never_calls_provider(string mode, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        var gateway = new FakeGateway();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (mode == "paid") (await context.Invoices.SingleAsync()).Status = "Paid";
            if (mode is "zero" or "fractional")
            {
                foreach (var line in await context.InvoiceDetails.ToListAsync()) line.Amount = mode == "zero" ? 0 : 0.1m;
            }
            await context.SaveChangesAsync();
        }
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(expected, (await client.PostAsync($"/api/invoices/{(mode == "missing" ? 999 : invoiceId)}/payment-link", null)).StatusCode);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Disabled_payos_returns_unavailable_without_creating_request()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync($"/api/invoices/{invoiceId}/payment-link", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentRequests.CountAsync());
    }

    private class FakeGateway : IInvoiceCheckoutGateway
    {
        public int Calls;
        public decimal LastAmount;
        public DateTime LastExpiry;
        public string Mode = "ok";
        public string RemoteState = "EXPIRED";
        public TaskCompletionSource? Started;
        public TaskCompletionSource? Release;
        public async Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken)
        {
            Calls++; LastAmount = amount; LastExpiry = expiresUtc;
            if (Release is not null)
            {
                Started!.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            if (Mode == "timeout") throw new TimeoutException("Sensitive provider error must not be returned.");
            if (Mode == "reject") throw new CheckoutCreationRejectedException();
            return new CheckoutLink(orderCode, Mode == "mismatch" ? 1 : amount, "VND", "link-" + orderCode,
                "https://pay.payos.vn/web/" + orderCode);
        }
        public Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken) =>
            Task.FromResult(new CheckoutStatus(RemoteState, RemoteState == "PAID" ? 355000 : 0));
    }
}
