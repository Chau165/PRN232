using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayOS;
using PRN232.Application.Interfaces;
using PRN232.Application.DTOs;
using PRN232.Application.Policies;
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
        ["orderCode"] = 123L, ["amount"] = 355000L, ["description"] = "HOADON", ["accountNumber"] = "0000000000",
        ["reference"] = "TEST-REF-1", ["transactionDateTime"] = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), ["currency"] = "VND",
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
    public async Task Valid_anonymous_webhook_settles_atomically_and_is_idempotent()
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
            Assert.Equal("Settled", receipt.Outcome);
            Assert.Equal(355000m, receipt.Amount);
            Assert.NotNull(receipt.PaymentRequestId);
            receivedAt = receipt.ReceivedAtUtc;
            var request = await context.PaymentRequests.SingleAsync();
            Assert.Equal("Succeeded", request.Status);
            Assert.Equal(receivedAt, request.WebhookReceivedAtUtc);
            Assert.Equal("TEST-REF-1", request.ProviderReference);
            Assert.NotNull(request.PaymentId);
            Assert.Equal(receivedAt, request.ConfirmedAtUtc);
            Assert.Equal("Paid", (await context.Invoices.SingleAsync()).Status);
            var payment = await context.Payments.SingleAsync(item => item.InvoiceId != null);
            Assert.Equal(request.PaymentId, payment.PaymentId);
            Assert.Equal("PayOS", payment.PaymentMethod);
            Assert.Equal("Success", payment.Status);
            Assert.Equal(request.Amount, payment.Amount);
            Assert.True(PRN232.Application.Policies.PayOSTransactionTime.TryGetUtc(receipt.TransactionDateTime, out var expectedPaidAt));
            Assert.Equal(expectedPaidAt, payment.PaidAtUtc);
            Assert.Equal(2, await context.Notifications.CountAsync(notification => notification.Title.Contains("payOS")));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.PaymentWebhookReceipts.CountAsync());
        Assert.Equal(receivedAt, (await verify.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
        Assert.Equal(1, await verify.Payments.CountAsync(payment => payment.InvoiceId != null));
        Assert.Equal(2, await verify.Notifications.CountAsync(notification => notification.Title.Contains("payOS")));
        var requestId = (await verify.PaymentRequests.SingleAsync()).PaymentRequestId;
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var status = (await client.GetFromJsonAsync<PaymentRequestStatusDto>($"/api/payment-requests/{requestId}"))!;
        Assert.Equal("Succeeded", status.Status);
        Assert.Equal("Paid", status.InvoiceStatus);
        Assert.NotNull(status.PaymentId);
        Assert.NotNull(status.PaidAtUtc);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        var month = today.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        var revenue = (await client.GetFromJsonAsync<RevenueReportDto>($"/api/reports/revenue?fromMonth={month}&toMonth={month}"))!;
        Assert.Equal(355000m, revenue.TotalCollected);
        Assert.Equal(1, revenue.PaymentCount);
        var debt = (await client.GetFromJsonAsync<ReceivablesReportDto>("/api/reports/receivables"))!;
        Assert.Equal(0m, debt.OutstandingAmount);
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
        Assert.Equal(355000m, receipt.Amount);
        Assert.Equal("Succeeded", (await context.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(1, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
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
        Assert.Equal("DuplicatePayment", (await context.PaymentWebhookReceipts.SingleAsync(receipt => receipt.Reference == "TEST-REF-2")).Outcome);
        Assert.Equal(1, await context.PaymentReviewCases.CountAsync());
        Assert.Equal("TEST-REF-1", (await context.PaymentRequests.SingleAsync()).ProviderReference);
        Assert.Equal("Succeeded", (await context.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(1, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    private static async Task SeedAsync(IServiceProvider services, HttpClient client)
    {
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.PaymentRequests.Add(new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = 10, Amount = 355000, OrderCode = 123,
            PaymentLinkId = "test-link", Status = "Pending", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("future")]
    [InlineData("before-request")]
    public async Task Invalid_transaction_time_requires_review_without_inventing_paid_at(string scenario)
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var data = Data();
        data["transactionDateTime"] = scenario switch
        {
            "missing" => "", "invalid" => "not-a-timestamp",
            "future" => DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            _ => "2020-01-01 00:00:00"
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(data))).StatusCode);
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("RequiresReview", (await context.PaymentWebhookReceipts.SingleAsync()).Outcome);
        Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    [Fact]
    public async Task Delayed_webhook_can_settle_payment_that_happened_before_checkout_expiry()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await context.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = DateTime.UtcNow.AddHours(-2);
            request.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
            await context.SaveChangesAsync();
        }
        var data = Data();
        var paidAt = DateTime.UtcNow.AddMinutes(-90);
        data["transactionDateTime"] = paidAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, await Signed(data))).StatusCode);
        using var verifyScope = app.Services.CreateScope();
        var payment = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.SingleAsync(item => item.InvoiceId != null);
        Assert.True(PayOSTransactionTime.TryGetUtc((string)data["transactionDateTime"], out var expected));
        Assert.Equal(expected, payment.PaidAtUtc);
    }

    [Fact]
    public async Task Settlement_failure_rolls_back_invoice_payment_request_receipt_and_notifications_then_retry_succeeds()
    {
        using var factory = new BookingsApiFactory();
        var failure = new NotificationFailure();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            Configure(builder);
            builder.ConfigureTestServices(services =>
            {
                var original = services.Last(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>));
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddScoped(provider =>
                {
                    var options = (DbContextOptions<AppDbContext>)(original.ImplementationInstance ?? original.ImplementationFactory!(provider));
                    return new DbContextOptionsBuilder<AppDbContext>(options).AddInterceptors(failure).Options;
                });
            });
        });
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var payload = await Signed(Data());
        failure.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        Assert.True(failure.Triggered);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Unpaid", (await context.Invoices.SingleAsync()).Status);
            Assert.Equal("Pending", (await context.PaymentRequests.SingleAsync()).Status);
            Assert.Null((await context.PaymentRequests.SingleAsync()).PaymentId);
            Assert.Equal(0, await context.PaymentWebhookReceipts.CountAsync());
            Assert.Equal(0, await context.Payments.CountAsync(payment => payment.InvoiceId != null));
            Assert.Equal(0, await context.Notifications.CountAsync(notification => notification.Title.Contains("payOS")));
        }
        failure.Fail = false;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        using var retryScope = app.Services.CreateScope();
        var retry = retryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Paid", (await retry.Invoices.SingleAsync()).Status);
        Assert.Equal(1, await retry.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    private class NotificationFailure : SaveChangesInterceptor
    {
        public bool Fail;
        public bool Triggered;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Fail && eventData.Context!.ChangeTracker.Entries<Notifications>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.Title.Contains("payOS")))
            {
                Triggered = true;
                throw new DbUpdateException("Deliberate test failure before settlement save.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Replayed_verified_B7_receipt_is_settled_once_without_replacing_original_receipt_time()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(Configure);
        using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var data = Data();
        var receivedAt = DateTime.UtcNow;
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await context.PaymentRequests.SingleAsync();
            var transactionTime = (string)data["transactionDateTime"];
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
            {
                OrderCode = 123L, PaymentLinkId = "test-link", Amount = 355000m, Currency = "VND",
                Code = "00", Reference = "TEST-REF-1", TransactionDateTime = transactionTime
            }))));
            context.PaymentWebhookReceipts.Add(new PaymentWebhookReceipts
            {
                Reference = "TEST-REF-1", OrderCode = 123, PaymentLinkId = "test-link", Amount = 355000,
                Currency = "VND", DataCode = "00", TransactionDateTime = transactionTime,
                Fingerprint = fingerprint, Outcome = "Verified", ReceivedAtUtc = receivedAt,
                PaymentRequestId = request.PaymentRequestId
            });
            request.WebhookReceivedAtUtc = receivedAt;
            request.ProviderReference = "TEST-REF-1";
            await context.SaveChangesAsync();
        }
        var payload = await Signed(data);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = await verify.PaymentWebhookReceipts.SingleAsync();
        Assert.Equal("Settled", receipt.Outcome);
        Assert.Equal(receivedAt, receipt.ReceivedAtUtc);
        Assert.Equal(receivedAt, (await verify.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
        Assert.Equal("Succeeded", (await verify.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(1, await verify.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, false)]
    [InlineData(-60, false)]
    [InlineData(50, false)]
    public async Task Invoice_minute_timestamp_preserves_expiry_and_duplicate_payment_detection(int seconds, bool accepted)
    {
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(Configure); using var client = app.CreateClient();
        await SeedAsync(app.Services, client);
        var minute = DateTime.UtcNow.AddMinutes(-5); minute = new DateTime(minute.Ticks - minute.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var request = await db.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = minute.AddSeconds(19.548336); request.ExpiresAtUtc = minute.AddSeconds(50); await db.SaveChangesAsync();
        }
        var data = Data(); data["transactionDateTime"] = new DateTimeOffset(minute.AddSeconds(seconds)).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        var payload = await Signed(data);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, payload)).StatusCode);
        if (accepted)
        {
            var extra = new Dictionary<string, object>(data); extra["reference"] = "MINUTE-EXTRA";
            var duplicate = await Signed(extra);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, duplicate)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, duplicate)).StatusCode);
        }
        using var check = app.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(accepted ? "Paid" : "Unpaid", (await context.Invoices.SingleAsync()).Status);
        Assert.Equal(accepted ? "Succeeded" : "RequiresReview", (await context.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(accepted ? 1 : 0, await context.Payments.CountAsync(p => p.InvoiceId != null));
        Assert.Equal(accepted ? 1 : 0, await context.PaymentReviewCases.CountAsync());
        if (accepted) Assert.Equal(minute, (await context.Payments.SingleAsync(p => p.InvoiceId != null)).PaidAtUtc);
    }
}
