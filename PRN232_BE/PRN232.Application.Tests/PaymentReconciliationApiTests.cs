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
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class PaymentReconciliationApiTests
{
    private const decimal Amount = 355000m;
    private static string Url(int id) => $"/api/payment-requests/{id}/reconcile";
    private sealed class Gateway : IPayOSReconciliationGateway
    {
        public int Calls;
        public Exception? Error;
        public PayOSRemoteSnapshot? Snapshot = new(123, "recovered-link", Amount, 0, Amount, "PENDING", []);
        public Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken)
        {
            Calls++;
            if (Error is not null) throw Error;
            return Task.FromResult(Snapshot);
        }
    }

    private static void Configure(IWebHostBuilder builder, Gateway gateway) => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOS:Enabled"] = "true", ["PayOS:ClientId"] = "test-client", ["PayOS:ApiKey"] = "test-key",
            ["PayOS:ChecksumKey"] = "test-checksum", ["PayOS:ReturnUrl"] = "https://example.test/return",
            ["PayOS:CancelUrl"] = "https://example.test/cancel", ["PayOS:ReconciliationEnabled"] = "false"
        })).ConfigureTestServices(services =>
        {
            services.RemoveAll<IPayOSReconciliationGateway>();
            services.AddSingleton<IPayOSReconciliationGateway>(gateway);
        });

    private static async Task<int> SeedAsync(IServiceProvider services, HttpClient client, bool expired = false)
    {
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var request = new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = 10, Amount = Amount, OrderCode = 123,
            Status = "Creating", CreatedAtUtc = now.AddHours(-1), UpdatedAtUtc = now.AddMinutes(-2),
            ExpiresAtUtc = expired ? now.AddMinutes(-1) : now.AddMinutes(30), LastErrorCode = "CREATE_RESULT_UNKNOWN"
        };
        db.PaymentRequests.Add(request);
        await db.SaveChangesAsync();
        return request.PaymentRequestId;
    }

    [Fact]
    public async Task Creating_recovers_existing_link_without_creating_a_new_order_or_payment()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        var response = await client.PostAsync(Url(id), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaymentReconciliationDto>();
        Assert.Equal("RecoveredPending", result!.Result);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.PaymentRequests.SingleAsync();
        Assert.Equal("Pending", request.Status);
        Assert.Equal(123, request.OrderCode);
        Assert.Equal("recovered-link", request.PaymentLinkId);
        Assert.Equal("https://pay.payos.vn/web/recovered-link", request.CheckoutUrl);
        Assert.Null(request.LastErrorCode);
        Assert.Equal(0, await db.Payments.CountAsync(payment => payment.InvoiceId != null));
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync(Url(id), null)).StatusCode);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Missing_webhook_paid_lookup_settles_once_and_late_equivalent_webhook_is_deduplicated()
    {
        var paidAt = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var gateway = new Gateway { Snapshot = new(123, "recovered-link", Amount, Amount, 0, "PAID",
            [new("reference-1", Amount, paidAt)]) };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(Url(id), null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(Url(id), null)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Paid", (await db.Invoices.SingleAsync()).Status);
            var request = await db.PaymentRequests.SingleAsync();
            Assert.Equal("Succeeded", request.Status);
            Assert.Null(request.WebhookReceivedAtUtc);
            Assert.NotNull(request.ConfirmedAtUtc);
            var payment = await db.Payments.SingleAsync(item => item.InvoiceId != null);
            Assert.True(PayOSTransactionTime.TryGetUtc(paidAt, out var actual));
            Assert.Equal(actual, payment.PaidAtUtc);
            Assert.Equal("Reconciliation", (await db.PaymentWebhookReceipts.SingleAsync()).Source);
            Assert.Equal(2, await db.Notifications.CountAsync(item => item.Title.Contains("payOS")));
            // Same actual transfer, different equivalent representation of its timestamp.
            var receipt = new PaymentWebhookReceipts
            {
                OrderCode = 123, PaymentLinkId = "recovered-link", Amount = Amount, Currency = "VND", DataCode = "00",
                Reference = "reference-1", TransactionDateTime = actual.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
                ReceivedAtUtc = DateTime.UtcNow
            };
            receipt.Fingerprint = PayOSReceiptFactory.Fingerprint(receipt);
            await scope.ServiceProvider.GetRequiredService<IPayOSWebhookRepository>().RecordAsync(receipt, true, default);
        }
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.Payments.CountAsync(item => item.InvoiceId != null));
        Assert.Equal("Settled", (await verify.PaymentWebhookReceipts.SingleAsync()).Outcome);
        Assert.NotNull((await verify.PaymentRequests.SingleAsync()).WebhookReceivedAtUtc);
        Assert.Equal(1, gateway.Calls);
    }

    [Theory]
    [InlineData("timeout", false, "Creating", HttpStatusCode.Accepted)]
    [InlineData("missing", false, "Creating", HttpStatusCode.Accepted)]
    [InlineData("missing", true, "RequiresReview", HttpStatusCode.OK)]
    [InlineData("timeout", true, "Creating", HttpStatusCode.Accepted)]
    public async Task Ambiguous_provider_lookup_never_releases_order_or_records_money(string mode, bool expired,
        string expectedStatus, HttpStatusCode httpStatus)
    {
        var gateway = new Gateway { Snapshot = null, Error = mode == "timeout" ? new TimeoutException("private provider error") : null };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client, expired);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(httpStatus, (await client.PostAsync(Url(id), null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.PaymentRequests.SingleAsync();
        Assert.Equal(expectedStatus, request.Status);
        Assert.DoesNotContain("private", request.LastErrorMessage!);
        Assert.Equal("Unpaid", (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await db.Payments.CountAsync(item => item.InvoiceId != null));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("order")]
    [InlineData("link")]
    [InlineData("partial")]
    [InlineData("multiple")]
    [InlineData("time")]
    public async Task Inconsistent_or_ambiguous_paid_lookup_requires_review(string mismatch)
    {
        var transfers = new List<PayOSRemoteTransfer> { new("reference-1", Amount, DateTime.UtcNow.AddMinutes(-1).ToString("O")) };
        var snapshot = new PayOSRemoteSnapshot(123, "recovered-link", Amount, Amount, 0, "PAID", transfers);
        if (mismatch == "amount") snapshot = snapshot with { Amount = 1 };
        if (mismatch == "order") snapshot = snapshot with { OrderCode = 999 };
        if (mismatch == "link") snapshot = snapshot with { LinkId = "../invalid" };
        if (mismatch == "partial") snapshot = snapshot with { AmountPaid = 1, AmountRemaining = Amount - 1 };
        if (mismatch == "multiple") transfers.Add(new("reference-2", Amount, transfers[0].TransactionDateTime));
        if (mismatch == "time") transfers[0] = transfers[0] with { TransactionDateTime = "invalid" };
        var gateway = new Gateway { Snapshot = snapshot };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(Url(id), null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("RequiresReview", (await db.PaymentRequests.SingleAsync()).Status);
        Assert.Equal("Unpaid", (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await db.Payments.CountAsync(item => item.InvoiceId != null));
    }

    [Theory]
    [InlineData("CANCELLED", "Cancelled")]
    [InlineData("EXPIRED", "Expired")]
    public async Task Confirmed_closed_unpaid_order_releases_active_slot(string providerStatus, string localStatus)
    {
        var gateway = new Gateway { Snapshot = new(123, "recovered-link", Amount, 0, Amount, providerStatus, []) };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client, true);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        await client.PostAsync(Url(id), null);
        using var scope = app.Services.CreateScope();
        Assert.Equal(localStatus, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Authorization_uses_current_property_owner_and_rejects_customer_before_provider_call()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        var managerToken = client.DefaultRequestHeaders.Authorization;
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(Url(id), null)).StatusCode);
        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        var managerBToken = client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(Url(id), null)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.Where(item => item.PropertyId == 1).ToListAsync());
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = managerToken;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(Url(id), null)).StatusCode);
        Assert.Equal(0, gateway.Calls);
        client.DefaultRequestHeaders.Authorization = managerBToken;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(Url(id), null)).StatusCode);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Durable_verified_receipt_is_replayed_without_calling_provider()
    {
        var gateway = new Gateway { Error = new TimeoutException() };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        var receivedAt = DateTime.UtcNow.AddSeconds(-10);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.PaymentRequests.SingleAsync();
            request.Status = "Pending";
            request.PaymentLinkId = "recovered-link";
            var receipt = new PaymentWebhookReceipts
            {
                PaymentRequestId = id, OrderCode = 123, PaymentLinkId = "recovered-link", Amount = Amount,
                Currency = "VND", DataCode = "00", Reference = "stored-reference", Outcome = "Verified",
                TransactionDateTime = DateTime.UtcNow.AddMinutes(-1).ToString("O"), ReceivedAtUtc = receivedAt
            };
            receipt.Fingerprint = PayOSReceiptFactory.Fingerprint(receipt);
            db.PaymentWebhookReceipts.Add(receipt);
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsync(Url(id), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ReplayedReceipt", (await response.Content.ReadFromJsonAsync<PaymentReconciliationDto>())!.Result);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Succeeded", (await verify.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(receivedAt, (await verify.PaymentWebhookReceipts.SingleAsync()).ReceivedAtUtc);
        Assert.True((await verify.PaymentRequests.SingleAsync()).ConfirmedAtUtc > receivedAt);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Background_batch_is_opt_in_bounded_and_moves_past_a_timed_out_request()
    {
        var gateway = new Gateway { Error = new TimeoutException() };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        int secondId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var first = await db.PaymentRequests.SingleAsync();
            var invoice = await db.Invoices.SingleAsync();
            var second = new PaymentRequests
            {
                Invoice = new Invoices { ContractId = invoice.ContractId, InvoiceMonth = invoice.InvoiceMonth.AddMonths(1),
                    DueDate = invoice.DueDate.AddMonths(1), Status = "Unpaid", InvoiceDetails =
                    [new InvoiceDetails { ItemType = "Rent", Description = "Rent", Amount = Amount }] },
                CustomerId = 10, Amount = Amount, OrderCode = 124, Status = "Creating",
                CreatedAtUtc = first.CreatedAtUtc, UpdatedAtUtc = first.UpdatedAtUtc.AddSeconds(1), ExpiresAtUtc = first.ExpiresAtUtc
            };
            db.PaymentRequests.Add(second);
            await db.SaveChangesAsync();
            secondId = second.PaymentRequestId;
        }
        var settings = app.Services.GetRequiredService<PayOSSettings>();
        using (var scope = app.Services.CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>().RunBatchAsync(default));
        Assert.Equal(0, gateway.Calls);
        settings.ReconciliationEnabled = true;
        settings.ReconciliationBatchSize = 1;
        using (var scope = app.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>().RunBatchAsync(default));
        using (var scope = app.Services.CreateScope())
        {
            var due = await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationRepository>()
                .FindDueAsync(DateTime.UtcNow.AddMinutes(-1), 1, default);
            Assert.Equal(new[] { secondId }, due);
        }
        using (var scope = app.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>().RunBatchAsync(default));
        Assert.Equal(2, gateway.Calls);
    }
}
