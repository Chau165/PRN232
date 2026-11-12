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
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Application.Services;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSCashApiTests
{
    private const decimal Amount = 3355000;
    private sealed class Gateway : IPayOSReconciliationGateway, IPayOSCancellationGateway
    {
        public PayOSRemoteSnapshot? Before = new(123, "cash-link", Amount, 0, Amount, "PENDING", []);
        public PayOSRemoteSnapshot? After = new(123, "cash-link", Amount, 0, Amount, "CANCELLED", []);
        public Exception? CancelError;
        public bool LookupError;
        public int Lookups;
        public int Cancels;
        public Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken)
        {
            Lookups++;
            if (LookupError) throw new TimeoutException("private provider error");
            return Task.FromResult(Lookups == 1 ? Before : After);
        }
        public Task CancelAsync(long orderCode, CancellationToken cancellationToken)
        {
            Cancels++;
            if (CancelError is not null) throw CancelError;
            return Task.CompletedTask;
        }
    }

    private static void Configure(IWebHostBuilder builder, Gateway gateway) => builder
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOS:Enabled"] = "true", ["PayOS:ReconciliationEnabled"] = "false",
            ["PayOS:ClientId"] = "test-client", ["PayOS:ApiKey"] = "test-key", ["PayOS:ChecksumKey"] = "test-checksum",
            ["PayOS:ReturnUrl"] = "https://example.test/return", ["PayOS:CancelUrl"] = "https://example.test/cancel"
        })).ConfigureTestServices(services =>
        {
            services.RemoveAll<IPayOSReconciliationGateway>();
            services.RemoveAll<IPayOSCancellationGateway>();
            services.AddSingleton<IPayOSReconciliationGateway>(gateway);
            services.AddSingleton<IPayOSCancellationGateway>(gateway);
        });

    private static async Task<int> SeedAsync(IServiceProvider services, HttpClient client, string status = "Pending")
    {
        var id = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PaymentRequests.Add(new PaymentRequests
        {
            InvoiceId = id, CustomerId = 10, Amount = Amount, OrderCode = 123, Status = status,
            PaymentLinkId = status == "Creating" ? null : "cash-link", CheckoutUrl = "https://pay.payos.vn/web/cash-link",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1), UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static PayOSRemoteSnapshot Paid() => new(123, "cash-link", Amount, Amount, 0, "PAID",
        [new("online-reference", Amount, DateTime.UtcNow.AddMinutes(-1).ToString("O"))]);

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("Pending", true)]
    [InlineData("Creating", false)]
    public async Task Cash_cancels_old_order_and_requires_confirmed_closed_unpaid_state(string status, bool lostCancelReply)
    {
        var gateway = new Gateway { CancelError = lostCancelReply ? new TimeoutException() : null };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client, status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        Assert.Equal(1, gateway.Cancels);
        Assert.Equal(2, gateway.Lookups);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Cancelled", (await db.PaymentRequests.SingleAsync()).Status);
        Assert.Equal("Paid", (await db.Invoices.SingleAsync()).Status);
        var payment = await db.Payments.SingleAsync(item => item.InvoiceId != null);
        Assert.Equal("Cash", payment.PaymentMethod);
        Assert.Equal(Amount, payment.Amount);
        Assert.NotNull(payment.PaidAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        Assert.Equal(1, gateway.Cancels);
    }

    [Theory]
    [InlineData("cancel-timeout")]
    [InlineData("not-closed")]
    [InlineData("lookup-timeout")]
    [InlineData("missing")]
    [InlineData("mismatch")]
    [InlineData("partial")]
    public async Task Cash_is_never_recorded_when_cancellation_or_money_is_uncertain(string scenario)
    {
        var gateway = new Gateway();
        if (scenario is "cancel-timeout" or "not-closed") gateway.After = gateway.Before;
        if (scenario == "cancel-timeout") gateway.CancelError = new TimeoutException();
        if (scenario == "lookup-timeout") gateway.LookupError = true;
        if (scenario == "missing") gateway.Before = null;
        if (scenario == "mismatch") gateway.Before = gateway.Before! with { Amount = 1 };
        if (scenario == "partial") gateway.Before = gateway.Before! with { AmountPaid = 1, AmountRemaining = Amount - 1 };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        var response = await client.PostAsync($"/api/invoices/{id}/record-cash", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("private provider", await response.Content.ReadAsStringAsync());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Unpaid", (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await db.Payments.CountAsync(item => item.InvoiceId != null));
        if (scenario is "missing" or "mismatch" or "partial" or "lookup-timeout") Assert.Equal(0, gateway.Cancels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Online_payment_before_or_during_cancel_wins_and_cash_is_rejected(bool duringCancel)
    {
        var gateway = new Gateway();
        if (duringCancel) gateway.After = Paid();
        else gateway.Before = Paid();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Paid", (await db.Invoices.SingleAsync()).Status);
        Assert.Equal("Succeeded", (await db.PaymentRequests.SingleAsync()).Status);
        Assert.Equal("PayOS", (await db.Payments.SingleAsync(item => item.InvoiceId != null)).PaymentMethod);
        Assert.Equal(duringCancel ? 1 : 0, gateway.Cancels);
    }

    [Fact]
    public async Task Foreign_manager_cannot_cancel_checkout_or_record_cash()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        Assert.Equal(0, gateway.Lookups);
        Assert.Equal(0, gateway.Cancels);
    }

    private sealed class NewCheckoutAfterPreparation(ICashCheckoutCoordinator inner, AppDbContext db) : ICashCheckoutCoordinator
    {
        public async Task PrepareAsync(int invoiceId, int? ownerId, CancellationToken cancellationToken)
        {
            await inner.PrepareAsync(invoiceId, ownerId, cancellationToken);
            db.PaymentRequests.Add(new PaymentRequests
            {
                InvoiceId = invoiceId, CustomerId = 10, Amount = Amount, OrderCode = 124, Status = "Creating",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task A_new_online_request_between_cancel_and_cash_commit_blocks_cash()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            Configure(builder, gateway);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICashCheckoutCoordinator>();
                services.AddScoped<ICashCheckoutCoordinator>(provider => new NewCheckoutAfterPreparation(
                    ActivatorUtilities.CreateInstance<CashCheckoutCoordinator>(provider), provider.GetRequiredService<AppDbContext>()));
            });
        });
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Unpaid", (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await db.Payments.CountAsync(item => item.InvoiceId != null));
        Assert.Equal("Creating", (await db.PaymentRequests.SingleAsync(item => item.OrderCode == 124)).Status);
    }

    [Theory]
    [InlineData("customer@example.test")]
    [InlineData("manager@example.test")]
    public async Task Review_endpoints_are_admin_only(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/payment-reviews")).StatusCode);
        await BookingsApiTests.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/payment-reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/payment-reviews/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/payment-reviews/1/resolve",
            new ResolvePaymentReviewDto("NotDuplicate", "Investigated payment", null))).StatusCode);
    }

    [Fact]
    public async Task Additional_online_money_after_cash_creates_one_admin_review_and_can_be_resolved_without_changing_revenue()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var id = await SeedAsync(app.Services, client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/invoices/{id}/record-cash", null)).StatusCode);
        var receivedAt = DateTime.UtcNow;
        using (var scope = app.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPayOSWebhookRepository>();
            PaymentWebhookReceipts Receipt() => new()
            {
                OrderCode = 123, PaymentLinkId = "cash-link", Reference = "late-reference", Amount = Amount,
                Currency = "VND", DataCode = "00", TransactionDateTime = receivedAt.AddMinutes(-1).ToString("O"),
                ReceivedAtUtc = receivedAt, Fingerprint = "test-fingerprint"
            };
            await repository.RecordAsync(Receipt(), true, default);
            await repository.RecordAsync(Receipt(), true, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.PaymentReviewCases.CountAsync());
            Assert.Equal(1, await db.Notifications.CountAsync(item => item.UserId == 9 && item.Title.Contains("trả trùng")));
            Assert.Equal(1, await db.Payments.CountAsync(item => item.InvoiceId != null));
            Assert.Equal("Cancelled", (await db.PaymentRequests.SingleAsync()).Status);
            Assert.Equal("DuplicatePayment", (await db.PaymentWebhookReceipts.SingleAsync()).Outcome);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/payment-reviews")).StatusCode);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        var reviews = (await client.GetFromJsonAsync<List<PaymentReviewDto>>("/api/payment-reviews"))!;
        var review = Assert.Single(reviews);
        Assert.Equal("Cash", review.ExistingPaymentMethod);
        Assert.Equal(Amount, review.AdditionalAmount);
        Assert.Equal("Open", review.Status);
        var invalid = await client.PostAsJsonAsync($"/api/payment-reviews/{review.Id}/resolve",
            new ResolvePaymentReviewDto("RefundedExternally", "Checked refund", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var resolution = new ResolvePaymentReviewDto("RefundedExternally", "Refund completed outside the application", "bank-refund-reference");
        var resolved = await client.PostAsJsonAsync($"/api/payment-reviews/{review.Id}/resolve", resolution);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var result = (await resolved.Content.ReadFromJsonAsync<PaymentReviewDto>())!;
        Assert.Equal("Resolved", result.Status);
        Assert.Equal(9, result.ResolvedByAdminId);
        Assert.NotNull(result.ResolvedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/payment-reviews/{review.Id}/resolve", resolution)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<PaymentReviewDto>>("/api/payment-reviews"))!);
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Paid", (await verify.Invoices.SingleAsync()).Status);
        Assert.Equal(Amount, (await verify.Payments.SingleAsync(item => item.InvoiceId != null)).Amount);
        Assert.Equal(1, await verify.PaymentWebhookReceipts.CountAsync());
    }
}
