using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayOS;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.PaymentGateways;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSWebhookApiTests
{
    private const string Url = "/api/payments/payos/webhook";
    private const string Key = "test-only-webhook-checksum-key";
    private static void Configure(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOS:Enabled"] = "true", ["PayOS:ClientId"] = "test-client",
            ["PayOS:ApiKey"] = "test-api", ["PayOS:ChecksumKey"] = Key,
            ["PayOS:ReturnUrl"] = "https://frontend.example.test/return",
            ["PayOS:CancelUrl"] = "https://frontend.example.test/cancel"
        })).ConfigureTestServices(services =>
        {
            // Test host configuration callbacks run after Program's conditional
            // registration; explicitly use the real SDK verifier with test keys.
            services.RemoveAll<IPayOSWebhookVerifier>();
            services.AddSingleton(new PayOSClient("test-client", "test-api", Key));
            services.AddScoped<IPayOSWebhookVerifier, PayOSWebhookVerifier>();
        });

    private static Dictionary<string, object> Data() => new()
    {
        ["orderCode"] = 123L, ["amount"] = 3355000L, ["description"] = "HOADON", ["accountNumber"] = "0000000000",
        ["reference"] = "TEST-REF-1", ["transactionDateTime"] = "2026-11-11 10:00:00", ["currency"] = "VND",
        ["paymentLinkId"] = "test-link", ["code"] = "00", ["desc"] = "success",
        ["counterAccountBankId"] = "", ["counterAccountBankName"] = "", ["counterAccountName"] = "",
        ["counterAccountNumber"] = "", ["virtualAccountName"] = "", ["virtualAccountNumber"] = ""
    };

    private static Task<object> Signed(Dictionary<string, object> data, bool tamper = false)
    {
        // Independent test signer; production verification always uses the official SDK.
        var canonical = string.Join("&", data.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key + "=" + Convert.ToString(item.Value, CultureInfo.InvariantCulture)));
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        if (tamper) data["amount"] = 1L;
        return Task.FromResult<object>(new { code = "00", desc = "success", success = true, data, signature });
    }

    [Fact]
    public async Task Valid_anonymous_webhook_is_durable_idempotent_and_does_not_settle_invoice()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        client.DefaultRequestHeaders.Authorization = null;
        var payload = await Signed(Data());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        DateTime receivedAt;
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var receipt = await context.PaymentWebhookReceipts.SingleAsync();
            Assert.Equal("Verified", receipt.Outcome);
            Assert.Equal(3355000m, receipt.Amount);
            Assert.NotNull(receipt.PaymentRequestId);
            receivedAt = receipt.ReceivedAtUtc;
            var request = await context.PaymentRequests.SingleAsync();
            Assert.Equal("Pending", request.Status);
            Assert.Equal(receivedAt, request.WebhookReceivedAtUtc);
            Assert.Equal("TEST-REF-1", request.ProviderReference);
            Assert.Null(request.PaymentId);
            Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
            Assert.Equal(0, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.PaymentWebhookReceipts.CountAsync());
        Assert.Equal(receivedAt, (await verify.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("link")]
    [InlineData("expired")]
    [InlineData("cancelled")]
    [InlineData("paid")]
    public async Task Signed_but_inconsistent_receipts_are_flagged_without_settlement(string mismatch)
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var data = Data();
        if (mismatch == "amount") data["amount"] = 1L;
        if (mismatch == "currency") data["currency"] = "USD";
        if (mismatch == "link") data["paymentLinkId"] = "another-link";
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await context.PaymentRequests.SingleAsync();
            if (mismatch == "expired") { request.CreatedAtUtc = DateTime.UtcNow.AddHours(-2); request.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1); }
            if (mismatch == "cancelled") request.Status = "Cancelled";
            if (mismatch == "paid") (await context.Invoices.SingleAsync()).Status = "Paid";
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(data))).StatusCode);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("RequiresReview", (await verify.PaymentWebhookReceipts.SingleAsync()).Outcome);
        Assert.Equal(mismatch == "cancelled" ? "Cancelled" : "RequiresReview", (await verify.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(0, await verify.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    [Fact]
    public async Task Tampered_or_unsigned_payload_never_writes_a_receipt()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url, await Signed(Data(), tamper: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url, new { data = Data() })).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await context.PaymentWebhookReceipts.CountAsync());
        Assert.Null((await context.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
    }

    [Fact]
    public async Task Unknown_order_is_retained_without_modifying_another_checkout()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var data = Data(); data["orderCode"] = 999L;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(data))).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = await context.PaymentWebhookReceipts.SingleAsync();
        Assert.Equal("UnknownOrder", receipt.Outcome);
        Assert.Null(receipt.PaymentRequestId);
        Assert.Null((await context.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
    }

    [Fact]
    public async Task Same_reference_with_changed_signed_data_is_not_silently_accepted_as_duplicate()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(Data()))).StatusCode);
        var conflict = Data(); conflict["amount"] = 123L;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(conflict))).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = await context.PaymentWebhookReceipts.SingleAsync();
        Assert.Equal("RequiresReview", receipt.Outcome);
        Assert.NotNull(receipt.ConflictingFingerprint);
        Assert.Equal(3355000m, receipt.Amount);
        Assert.Equal("RequiresReview", (await context.PaymentRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Disabled_webhook_does_not_acknowledge_or_store_events()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(Url, await Signed(Data()))).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentWebhookReceipts.CountAsync());
    }

    [Fact]
    public async Task Signed_non_success_data_code_does_not_confirm_receipt()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var data = Data(); data["code"] = "99";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(data))).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("IgnoredStatus", (await context.PaymentWebhookReceipts.SingleAsync()).Outcome);
        Assert.Null((await context.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
        Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
    }

    [Fact]
    public async Task Altered_unsigned_success_envelope_cannot_poison_a_valid_payment_notice()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var payload = System.Text.Json.JsonSerializer.SerializeToNode(await Signed(Data()))!;
        payload["success"] = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await context.PaymentWebhookReceipts.CountAsync());
        Assert.Equal("Pending", (await context.PaymentRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Additional_transfer_reference_for_same_request_requires_review()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(Data()))).StatusCode);
        var additional = Data(); additional["reference"] = "TEST-REF-2";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(additional))).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await context.PaymentWebhookReceipts.CountAsync());
        Assert.Equal("RequiresReview", (await context.PaymentWebhookReceipts.SingleAsync(receipt => receipt.Reference == "TEST-REF-2")).Outcome);
        Assert.Equal("TEST-REF-1", (await context.PaymentRequests.SingleAsync()).ProviderReference);
        Assert.Equal("RequiresReview", (await context.PaymentRequests.SingleAsync()).Status);
    }

    private static async Task SeedAsync(IServiceProvider services, HttpClient client)
    {
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.PaymentRequests.Add(new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = 10, Amount = 3355000, OrderCode = 123,
            PaymentLinkId = "test-link", Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await context.SaveChangesAsync();
    }
}
