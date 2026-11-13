using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayOS;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.PaymentGateways;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingPayOSApiTests
{
    private const string Key = PayOSTestMerchant.Key;
    private const string Webhook = "/api/payments/payos/webhook";
    private sealed class Gateway : IBookingCheckoutGateway, IPayOSReconciliationGateway
    {
        public int Creates;
        public long Order;
        public decimal Amount;
        public DateTime Expires;
        public Exception? CreateError;
        public Exception? LookupError;
        public PayOSRemoteSnapshot? Remote;
        public Func<Task>? BeforeReply;
        public bool BadResponse;
        public async Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken)
        {
            Creates++; Order = orderCode; Amount = amount; Expires = expiresUtc;
            if (BeforeReply is not null) await BeforeReply();
            if (CreateError is not null) throw CreateError;
            var linkId = Creates == 1 ? "booking-link" : "booking-link-" + Creates;
            return new(orderCode, BadResponse ? amount + 1 : amount, "VND", linkId, "https://pay.payos.vn/web/" + linkId);
        }
        public Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken) => Task.FromResult(new CheckoutStatus("PENDING", 0));
        public Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken)
        {
            if (LookupError is not null) throw LookupError;
            return Task.FromResult<PayOSRemoteSnapshot?>(Remote ?? new PayOSRemoteSnapshot(Order, "booking-link", Amount, 0, Amount, "PENDING", []));
        }
    }

    private static void Configure(IWebHostBuilder builder, Gateway gateway) => builder.ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOS:Enabled"] = "true", ["PayOS:BookingPaymentsEnabled"] = "true", ["PayOS:ReconciliationEnabled"] = "false",
            ["PayOS:ClientId"] = "test-client", ["PayOS:ApiKey"] = "test-api", ["PayOS:ChecksumKey"] = Key,
            ["PayOS:ReturnUrl"] = "https://frontend.test/payment-result", ["PayOS:CancelUrl"] = "https://frontend.test/payment-result"
        })).ConfigureTestServices(services =>
        {
            services.RemoveAll<IBookingCheckoutGateway>(); services.RemoveAll<IPayOSReconciliationGateway>(); services.RemoveAll<IPayOSWebhookVerifier>();
            services.AddSingleton<IBookingCheckoutGateway>(gateway); services.AddSingleton<IPayOSReconciliationGateway>(gateway);
            services.AddSingleton(new PayOSClient("test-client", "test-api", Key));
            services.AddScoped<IPayOSWebhookVerifier, PayOSWebhookVerifier>();
        });

    private static async Task<BookingDto> CreateBookingAsync(HttpClient client)
    {
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDto>())!;
    }
    private static async Task<BookingPaymentLinkDto> CreateLinkAsync(HttpClient client, int bookingId)
    {
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/payment-link", new { amount = 1, customerId = 20 });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Accepted });
        return (await response.Content.ReadFromJsonAsync<BookingPaymentLinkDto>())!;
    }
    private static object Signed(Gateway gateway, string? paidAt = null, string reference = "booking-ref", string? mismatch = null)
    {
        var data = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["orderCode"] = gateway.Order, ["amount"] = (long)gateway.Amount, ["description"] = "COC PHONG", ["accountNumber"] = "0000000000",
            ["reference"] = reference, ["transactionDateTime"] = paidAt ?? DateTime.UtcNow.ToString("O"), ["currency"] = "VND",
            ["paymentLinkId"] = "booking-link", ["code"] = "00", ["desc"] = "success", ["counterAccountBankId"] = "",
            ["counterAccountBankName"] = "", ["counterAccountName"] = "", ["counterAccountNumber"] = "",
            ["virtualAccountName"] = "", ["virtualAccountNumber"] = ""
        };
        if (mismatch == "amount") data["amount"] = 1L;
        if (mismatch == "currency") data["currency"] = "USD";
        if (mismatch == "link") data["paymentLinkId"] = "other-link";
        if (mismatch == "time") data["transactionDateTime"] = "invalid";
        var canonical = string.Join("&", data.Select(item => item.Key + "=" + Convert.ToString(item.Value, CultureInfo.InvariantCulture)));
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new { code = "00", success = true, desc = "success", data, signature = mismatch == "signature" ? "invalid" : signature };
    }

    [Fact]
    public async Task Checkout_is_owned_server_priced_reused_and_expires_with_booking()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient();
        var booking = await CreateBookingAsync(client);
        var first = await CreateLinkAsync(client, booking.BookingId);
        var second = await CreateLinkAsync(client, booking.BookingId);
        Assert.Equal(3000000, first.Amount);
        Assert.Equal(booking.BookingId, first.BookingId);
        Assert.Equal(booking.ExpiryDate.Ticks, gateway.Expires.Ticks);
        Assert.Equal(first.PaymentRequestId, second.PaymentRequestId);
        Assert.True(second.Reused);
        Assert.Equal(1, gateway.Creates);
        var status = (await client.GetFromJsonAsync<PaymentRequestStatusDto>($"/api/payment-requests/{first.PaymentRequestId}"))!;
        Assert.Null(status.InvoiceId); Assert.Null(status.InvoiceStatus);
        Assert.Equal(booking.BookingId, status.BookingId); Assert.Equal("Pending", status.BookingStatus); Assert.True(status.CanOpenCheckout);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/pay", new { paymentMethod = "MoMo" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/bookings/{booking.BookingId}/cancel", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.CountAsync());
    }

    [Fact]
    public async Task Booking_rollout_flag_blocks_new_links_but_cannot_bypass_an_existing_payOS_request_using_mock_payment()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client);
        var settings = app.Services.GetRequiredService<PayOSSettings>(); settings.BookingPaymentsEnabled = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync($"/api/bookings/{booking.BookingId}/payment-link", null)).StatusCode);
        Assert.Equal(0, gateway.Creates);
        settings.BookingPaymentsEnabled = true;
        await CreateLinkAsync(client, booking.BookingId);
        settings.BookingPaymentsEnabled = false;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/pay", new { paymentMethod = "MoMo" })).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.CountAsync());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("other@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_the_customer_can_create_the_link(string? email, HttpStatusCode expected)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client);
        if (email is null) client.DefaultRequestHeaders.Authorization = null; else await BookingsApiTests.LoginAsync(client, email);
        Assert.Equal(expected, (await client.PostAsync($"/api/bookings/{booking.BookingId}/payment-link", null)).StatusCode);
        Assert.Equal(0, gateway.Creates);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("cancelled")]
    [InlineData("deposited")]
    [InlineData("amount")]
    [InlineData("room")]
    public async Task Invalid_bookings_never_create_provider_orders(string condition)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.Bookings.SingleAsync();
            if (condition == "expired") row.ExpiryDate = DateTime.UtcNow.AddSeconds(-1);
            if (condition == "cancelled") row.Status = "Cancelled";
            if (condition == "deposited") row.Status = "Deposited";
            if (condition == "amount") row.DepositAmount = 0.5m;
            if (condition == "room") (await db.Rooms.SingleAsync(item => item.RoomId == 1)).Status = "Maintenance";
            await db.SaveChangesAsync();
        }
        Assert.Equal(condition == "amount" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/bookings/{booking.BookingId}/payment-link", null)).StatusCode);
        Assert.Equal(0, gateway.Creates);
    }

    [Fact]
    public async Task Verified_webhook_settles_once_keeps_original_deadline_and_blocks_direct_cancellation()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        var payload = Signed(gateway);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, payload)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status);
            Assert.Equal(booking.ExpiryDate.Ticks, (await db.Bookings.SingleAsync()).ExpiryDate.Ticks);
            var payment = await db.Payments.SingleAsync(); Assert.Equal(booking.BookingId, payment.BookingId); Assert.Null(payment.InvoiceId);
            Assert.Equal("PayOS", payment.PaymentMethod); Assert.Equal(3000000, payment.Amount); Assert.NotNull(payment.PaidAtUtc);
            var request = await db.PaymentRequests.SingleAsync(); Assert.Equal("Succeeded", request.Status); Assert.Equal(payment.PaymentId, request.PaymentId);
            Assert.Equal("Settled", (await db.PaymentWebhookReceipts.SingleAsync()).Outcome);
            Assert.Equal(2, await db.Notifications.CountAsync(item => item.Title.Contains("qua payOS")));
        }
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/bookings/{booking.BookingId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payment-requests/{link.PaymentRequestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, Signed(gateway, reference: "additional-ref"))).StatusCode);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.Payments.CountAsync()); Assert.Equal(2, await verify.PaymentWebhookReceipts.CountAsync());
        Assert.Equal("Succeeded", (await verify.PaymentRequests.SingleAsync()).Status);
        Assert.Equal("RequiresReview", (await verify.PaymentWebhookReceipts.SingleAsync(item => item.Reference == "additional-ref")).Outcome);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("link")]
    [InlineData("time")]
    [InlineData("signature")]
    public async Task Invalid_webhooks_cannot_credit_a_booking(string mismatch)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); await CreateLinkAsync(client, booking.BookingId);
        Assert.Equal(mismatch == "signature" ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            (await client.PostAsJsonAsync(Webhook, Signed(gateway, mismatch: mismatch))).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Pending", (await db.Bookings.SingleAsync()).Status); Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal(mismatch == "signature" ? "Pending" : "RequiresReview", (await db.PaymentRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Delayed_callback_only_deposits_if_paid_on_time_and_room_not_reassigned(bool roomTaken, bool paidLate)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); await CreateLinkAsync(client, booking.BookingId);
        var now = DateTime.UtcNow;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var request = await db.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = now.AddMinutes(-5); request.UpdatedAtUtc = now.AddMinutes(-2); request.ExpiresAtUtc = now.AddMinutes(-1);
            (await db.Bookings.SingleAsync()).ExpiryDate = now.AddMinutes(-1);
            if (roomTaken) db.Bookings.Add(new Bookings { RoomId = 1, CustomerId = 20, DepositAmount = 3000000, Status = "Pending", ExpiryDate = now.AddMinutes(15) });
            await db.SaveChangesAsync();
        }
        var paymentTime = (paidLate ? now : now.AddMinutes(-2)).ToString("O");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, Signed(gateway, paymentTime))).StatusCode);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accepted = !roomTaken && !paidLate;
        Assert.Equal(accepted ? "Deposited" : "Pending", (await verify.Bookings.SingleAsync(item => item.BookingId == booking.BookingId)).Status);
        Assert.Equal(accepted ? 1 : 0, await verify.Payments.CountAsync());
        if (accepted)
        {
            await BookingsApiTests.LoginAsync(client, "other@example.test");
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 })).StatusCode);
        }
    }

    [Fact]
    public async Task Early_webhook_can_settle_before_the_create_response_persists_link_id()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client);
        gateway.BeforeReply = async () => Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, Signed(gateway))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/bookings/{booking.BookingId}/payment-link", null)).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Succeeded", (await db.PaymentRequests.SingleAsync()).Status); Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Theory]
    [InlineData("rejected", HttpStatusCode.BadGateway, "Create-failed")]
    [InlineData("timeout", HttpStatusCode.Accepted, "Creating")]
    [InlineData("mismatch", HttpStatusCode.Conflict, "RequiresReview")]
    public async Task Creation_failures_preserve_the_correct_uncertainty(string mode, HttpStatusCode expected, string status)
    {
        var gateway = new Gateway { CreateError = mode == "rejected" ? new CheckoutCreationRejectedException() : mode == "timeout" ? new TimeoutException() : null, BadResponse = mode == "mismatch" };
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client);
        Assert.Equal(expected, (await client.PostAsync($"/api/bookings/{booking.BookingId}/payment-link", null)).StatusCode);
        if (mode == "timeout") { await CreateLinkAsync(client, booking.BookingId); Assert.Equal(1, gateway.Creates); }
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(status, (await db.PaymentRequests.SingleAsync()).Status); Assert.Equal(0, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Reconciliation_recovers_creating_then_settles_missing_webhook_without_another_create()
    {
        var gateway = new Gateway { CreateError = new TimeoutException() };
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        async Task Due()
        {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.PaymentRequests.SingleAsync();
            row.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5); row.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
        }
        await Due();
        var recovered = await client.PostAsync($"/api/payment-requests/{link.PaymentRequestId}/reconcile", null);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode); Assert.Equal("Pending", (await recovered.Content.ReadFromJsonAsync<PaymentReconciliationDto>())!.Status);
        await Due();
        gateway.Remote = new(gateway.Order, "booking-link", gateway.Amount, gateway.Amount, 0, "PAID",
            [new("booking-ref", gateway.Amount, DateTime.UtcNow.AddMinutes(-1).ToString("O"))]);
        var settled = await client.PostAsync($"/api/payment-requests/{link.PaymentRequestId}/reconcile", null);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode); Assert.Equal("Succeeded", (await settled.Content.ReadFromJsonAsync<PaymentReconciliationDto>())!.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/payment-requests/{link.PaymentRequestId}/reconcile", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, Signed(gateway, gateway.Remote.Transactions[0].TransactionDateTime))).StatusCode);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.Payments.CountAsync()); Assert.Equal(1, gateway.Creates);
        Assert.Equal("Deposited", (await verify.Bookings.SingleAsync()).Status);
        Assert.Equal("Reconciliation", (await verify.PaymentWebhookReceipts.SingleAsync()).Source);
    }

    [Fact]
    public async Task Booking_get_and_reconcile_follow_the_current_manager_and_customer_scope()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        await BookingsApiTests.LoginAsync(client, "other@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/payment-requests/{link.PaymentRequestId}")).StatusCode);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payment-requests/{link.PaymentRequestId}")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.Where(item => item.PropertyId == 1).ToListAsync());
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/payment-requests/{link.PaymentRequestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/payment-requests/{link.PaymentRequestId}/reconcile", null)).StatusCode);
        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/payment-requests/{link.PaymentRequestId}")).StatusCode);
    }

    [Fact]
    public async Task Official_sdk_booking_gateway_and_verifier_work_with_signed_merchant_transport()
    {
        var gateway = new Gateway(); using var merchant = new PayOSTestMerchant();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder =>
        {
            Configure(builder, gateway);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PayOSClient>(); services.RemoveAll<IBookingCheckoutGateway>();
                services.AddSingleton(new PayOSClient(new PayOSOptions { ClientId = "test-client", ApiKey = "test-api", ChecksumKey = Key,
                    BaseUrl = "https://payos.test.invalid", HttpClient = new HttpClient(merchant), MaxRetries = 0 }));
                services.AddScoped<IBookingCheckoutGateway, PayOSBookingCheckoutGateway>();
            });
        });
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        Assert.Equal("Pending", link.Status);
        Assert.Equal(new DateTimeOffset(booking.ExpiryDate).ToUnixTimeSeconds(), merchant.ExpiresAt);
        Assert.True(merchant.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        merchant.Pay();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, merchant.Webhook())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, merchant.Webhook())).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, merchant.Creates); Assert.Equal(1, await db.Payments.CountAsync()); Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Lookup_timeout_preserves_booking_and_the_same_checkout_for_retry()
    {
        var gateway = new Gateway { CreateError = new TimeoutException(), LookupError = new TimeoutException() };
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.PaymentRequests.SingleAsync();
            row.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5); row.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
        }
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        var response = await client.PostAsync($"/api/payment-requests/{link.PaymentRequestId}/reconcile", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Creating", (await verify.PaymentRequests.SingleAsync()).Status);
        Assert.Equal("Pending", (await verify.Bookings.SingleAsync()).Status);
        Assert.Equal(1, gateway.Creates); Assert.Equal(0, await verify.Payments.CountAsync());
    }

    [Fact]
    public async Task Worker_reconciles_booking_candidates_and_confirmed_unpaid_closure_allows_a_fresh_attempt()
    {
        var gateway = new Gateway { CreateError = new TimeoutException() };
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var first = await CreateLinkAsync(client, booking.BookingId);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.PaymentRequests.SingleAsync();
            row.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5); row.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
        }
        gateway.Remote = new(gateway.Order, "booking-link", gateway.Amount, 0, gateway.Amount, "CANCELLED", []);
        app.Services.GetRequiredService<PayOSSettings>().ReconciliationEnabled = true;
        using (var scope = app.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>().RunBatchAsync(default));
        gateway.CreateError = null;
        var second = await CreateLinkAsync(client, booking.BookingId);
        Assert.NotEqual(first.PaymentRequestId, second.PaymentRequestId);
        Assert.Equal(2, gateway.Creates);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Cancelled", (await verify.PaymentRequests.SingleAsync(item => item.PaymentRequestId == first.PaymentRequestId)).Status);
        Assert.Equal("Pending", (await verify.Bookings.SingleAsync()).Status);
        Assert.Equal(0, await verify.Payments.CountAsync());
    }

    private sealed class NotificationFailure : SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<Notifications>().Any(item => item.State == EntityState.Added && item.Entity.Title.Contains("qua payOS")))
                throw new DbUpdateException("Test failure during booking settlement.");
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Failed_settlement_rolls_back_booking_payment_request_receipt_and_notifications_then_retry_succeeds()
    {
        var gateway = new Gateway(); var failure = new NotificationFailure();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder =>
        {
            Configure(builder, gateway);
            builder.ConfigureTestServices(services =>
            {
                var original = services.Last(item => item.ServiceType == typeof(DbContextOptions<AppDbContext>));
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddScoped(provider =>
                {
                    var options = (DbContextOptions<AppDbContext>)(original.ImplementationInstance ?? original.ImplementationFactory!(provider));
                    return new DbContextOptionsBuilder<AppDbContext>(options).AddInterceptors(failure).Options;
                });
            });
        });
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); await CreateLinkAsync(client, booking.BookingId);
        var payload = Signed(gateway); failure.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(Webhook, payload)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Pending", (await db.Bookings.SingleAsync()).Status);
            Assert.Equal("Pending", (await db.PaymentRequests.SingleAsync()).Status);
            Assert.Null((await db.PaymentRequests.SingleAsync()).PaymentId);
            Assert.Equal(0, await db.Payments.CountAsync()); Assert.Equal(0, await db.PaymentWebhookReceipts.CountAsync());
            Assert.Equal(0, await db.Notifications.CountAsync(item => item.Title.Contains("qua payOS")));
        }
        failure.Fail = false;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, payload)).StatusCode);
        using var verifyScope = app.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Deposited", (await verify.Bookings.SingleAsync()).Status); Assert.Equal(1, await verify.Payments.CountAsync());
    }

    [Fact]
    public async Task Same_minute_provider_timestamp_settles_booking_without_changing_original_paid_time()
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        var paid = DateTime.UtcNow.AddMinutes(-5); paid = new DateTime(paid.Ticks - paid.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.PaymentRequests.SingleAsync()).CreatedAtUtc = paid.AddSeconds(19.548336); await db.SaveChangesAsync();
        }
        var data = Signed(gateway, new DateTimeOffset(paid).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, data)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, data)).StatusCode);
        using var check = app.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Deposited", (await context.Bookings.SingleAsync()).Status);
        Assert.Equal("Succeeded", (await context.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(paid, (await context.Payments.SingleAsync()).PaidAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_can_recheck_legacy_minute_time_review_but_cannot_override_room_conflicts(bool roomBlocked)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); var link = await CreateLinkAsync(client, booking.BookingId);
        var now = DateTime.UtcNow; var paid = now.AddMinutes(-5);
        paid = new DateTime(paid.Ticks - paid.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var timestamp = new DateTimeOffset(paid).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var request = await db.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = paid.AddSeconds(19.548336); request.UpdatedAtUtc = now.AddMinutes(-2);
            request.Status = "RequiresReview"; request.LastErrorCode = "BOOKING_PAYMENT_REQUIRES_REVIEW";
            if (roomBlocked) (await db.Rooms.SingleAsync(r => r.RoomId == booking.RoomId)).Status = "Maintenance";
            var receipt = new PaymentWebhookReceipts { PaymentRequestId = request.PaymentRequestId, Source = "Reconciliation", OrderCode = gateway.Order,
                PaymentLinkId = "booking-link", Amount = gateway.Amount, Currency = "VND", DataCode = "00", Reference = "minute-review-ref",
                TransactionDateTime = timestamp, ReceivedAtUtc = now.AddMinutes(-3), Outcome = "RequiresReview" };
            receipt.Fingerprint = PRN232.Application.Policies.PayOSReceiptFactory.Fingerprint(receipt);
            db.PaymentWebhookReceipts.Add(receipt); await db.SaveChangesAsync();
        }
        gateway.Remote = new(gateway.Order, "booking-link", gateway.Amount, gateway.Amount, 0, "PAID", [new("minute-review-ref", gateway.Amount, timestamp)]);
        var url = $"/api/payment-requests/{link.PaymentRequestId}/reconcile";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(url + "?recheckBooking=true", null)).StatusCode);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(url + "?recheckBooking=true", null)).StatusCode);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        var untouched = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<PaymentReconciliationDto>())!;
        Assert.Equal("ManualReviewOrClosed", untouched.Result);
        var result = await client.PostAsync(url + "?recheckBooking=true", null); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        if (!roomBlocked) Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(url + "?recheckBooking=true", null)).StatusCode);
        using var check = app.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(roomBlocked ? "RequiresReview" : "Succeeded", (await context.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(roomBlocked ? "Pending" : "Deposited", (await context.Bookings.SingleAsync()).Status);
        Assert.Equal(roomBlocked ? 0 : 1, await context.Payments.CountAsync());
        Assert.Single(await context.PaymentWebhookReceipts.ToListAsync());
        if (!roomBlocked) Assert.Equal(paid, (await context.Payments.SingleAsync()).PaidAtUtc);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task Minute_tolerance_does_not_accept_older_seconds_previous_minutes_or_expiry_boundary(int seconds)
    {
        var gateway = new Gateway();
        using var factory = new BookingsApiFactory(); using var app = factory.WithWebHostBuilder(builder => Configure(builder, gateway));
        using var client = app.CreateClient(); var booking = await CreateBookingAsync(client); await CreateLinkAsync(client, booking.BookingId);
        var minute = DateTime.UtcNow.AddMinutes(-5); minute = new DateTime(minute.Ticks - minute.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var request = await db.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = minute.AddSeconds(19.548336); request.ExpiresAtUtc = minute.AddSeconds(50); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Webhook, Signed(gateway, minute.AddSeconds(seconds).ToString("O")))).StatusCode);
        using var check = app.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await context.PaymentRequests.SingleAsync();
        Assert.Equal("RequiresReview", stored.Status);
        Assert.Equal(PRN232.Application.Policies.BookingPaymentTimePolicy.ReviewCode, stored.LastErrorCode);
        Assert.Empty(await context.Payments.ToListAsync());
        Assert.Equal("Pending", (await context.Bookings.SingleAsync()).Status);
    }
}
