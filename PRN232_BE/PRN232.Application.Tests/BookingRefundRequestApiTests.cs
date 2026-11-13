using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class BookingRefundRequestApiTests
{
    private static readonly CreateBookingRefundDto Input = new("Không còn nhu cầu thuê", "970422", "001234567890", "TEST CUSTOMER");
    private static string Url(int bookingId) => $"/api/bookings/{bookingId}/refund-requests";

    internal static async Task<int> SeedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<PRN232.Api.Controllers.BookingsController> factory,
        string status = "Deposited", string method = "PayOS", bool evidence = true, int roomId = 1)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var booking = new Bookings { RoomId = roomId, CustomerId = 10, DepositAmount = 3000000, Status = status, ExpiryDate = now.AddDays(-30) };
        var payment = new Payments { Booking = booking, UserId = 10, Amount = 3000000, Status = "Success", PaymentMethod = method, PaidAtUtc = now.AddDays(-31) };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        if (evidence)
        {
            db.PaymentRequests.Add(new PaymentRequests { BookingId = booking.BookingId, PaymentId = payment.PaymentId,
                CustomerId = 10, Amount = 3000000, Currency = "VND", Provider = "PayOS", Status = "Succeeded",
                OrderCode = 123456789 + booking.BookingId, PaymentLinkId = "refund-source-" + booking.BookingId, CreatedAtUtc = now.AddDays(-31), UpdatedAtUtc = now,
                ConfirmedAtUtc = now });
            await db.SaveChangesAsync();
        }
        return booking.BookingId;
    }

    [Fact]
    public async Task Creates_pending_review_from_server_payment_and_keeps_room_and_financial_state_unchanged()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var bookingId = await SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var response = await client.PostAsJsonAsync(Url(bookingId), new { Input.Reason, Input.RecipientBankBin,
            Input.RecipientAccountNumber, Input.RecipientAccountName, requestedAmount = 9000000, customerId = 20, status = "Refunded", originalPaymentId = 999 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BookingRefundDto>())!;
        Assert.Equal((bookingId, 3000000m, "VND", "PendingReview"), (body.BookingId, body.RequestedAmount, body.Currency, body.Status));
        Assert.False(body.AccountNameVerified);
        Assert.Equal("****7890", body.MaskedRecipientAccountNumber);
        Assert.DoesNotContain(Input.RecipientAccountNumber, await response.Content.ReadAsStringAsync());
        Assert.Equal(DateTimeKind.Utc, body.CreatedAtUtc.Kind);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var detail = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.BookingRefundRequests.SingleAsync();
        Assert.Equal(10, stored.CustomerId);
        Assert.Equal(Input.RecipientAccountNumber, stored.RecipientAccountNumber);
        Assert.Null(stored.ApprovedAmount); Assert.Null(stored.ReviewedAtUtc); Assert.Null(stored.RefundedAtUtc);
        Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(0, await db.PayoutAttempts.CountAsync());
        Assert.Equal("Succeeded", (await db.PaymentRequests.SingleAsync()).Status);
        var admins = await db.Users.CountAsync(item => item.IsActive && item.Role!.RoleName == "Admin");
        Assert.Equal(admins, await db.Notifications.CountAsync(item => item.Title.Contains("Yêu cầu hoàn cọc")));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("other@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_the_booking_customer_can_submit(string? email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;
        if (email is not null) await LoginAsync(client, email);
        Assert.Equal(expected, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefundRequests.CountAsync());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Cancelled")]
    public async Task Non_deposited_booking_is_rejected(string status)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory, status);
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
    }

    [Theory]
    [InlineData("mock")]
    [InlineData("missing-evidence")]
    [InlineData("wrong-amount")]
    [InlineData("wrong-customer")]
    [InlineData("failed-payment")]
    [InlineData("multiple-payments")]
    [InlineData("evidence-amount")]
    [InlineData("unresolved")]
    public async Task Unverified_or_ambiguous_deposits_are_rejected(string scenario)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await SeedAsync(factory, method: scenario == "mock" ? "MoMo" : "PayOS", evidence: scenario != "missing-evidence");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var payment = await db.Payments.SingleAsync();
            if (scenario == "wrong-amount") payment.Amount = 1;
            if (scenario == "wrong-customer") payment.UserId = 20;
            if (scenario == "failed-payment") payment.Status = "Failed";
            if (scenario == "multiple-payments") db.Payments.Add(new Payments { BookingId = id, UserId = 10, Amount = 3000000, PaymentMethod = "PayOS", Status = "Success" });
            if (scenario == "evidence-amount") (await db.PaymentRequests.SingleAsync()).Amount = 1;
            if (scenario == "unresolved") (await db.PaymentRequests.SingleAsync()).Status = "RequiresReview";
            await db.SaveChangesAsync();
        }
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("long-reason")]
    [InlineData("bank")]
    [InlineData("account")]
    [InlineData("name")]
    public async Task Invalid_destination_and_reason_are_rejected(string scenario)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        var input = scenario switch { "reason" => Input with { Reason = " " }, "long-reason" => Input with { Reason = new string('a', 2001) },
            "bank" => Input with { RecipientBankBin = "bad" }, "account" => Input with { RecipientAccountNumber = "12;34" },
            _ => Input with { RecipientAccountName = "BAD\0NAME" } };
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url(id), input)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_submission_has_one_winner_and_does_not_duplicate_notifications()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var results = await Task.WhenAll(client.PostAsJsonAsync(Url(id), Input), client.PostAsJsonAsync(Url(id), Input));
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefundRequests.CountAsync());
    }

    [Theory]
    [InlineData("PendingReview")]
    [InlineData("Approved")]
    [InlineData("Refunding")]
    [InlineData("RequiresReview")]
    [InlineData("Refunded")]
    public async Task Existing_claim_prevents_another_request(string status)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var item = await db.BookingRefundRequests.SingleAsync();
            if (status != "PendingReview") { item.ApprovedAmount = item.RequestedAmount; item.ReviewedByAdminId = 9;
                item.ReviewedAtUtc = item.CreatedAtUtc; }
            if (status == "Refunded") item.RefundedAtUtc = item.CreatedAtUtc;
            item.Status = status; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
    }

    [Fact]
    public async Task Rejected_claim_allows_resubmission_but_other_customer_cannot_read_history()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var first = await client.PostAsJsonAsync(Url(id), Input);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var item = await db.BookingRefundRequests.SingleAsync();
            item.Status = "Rejected"; item.ReviewedByAdminId = 9; item.ReviewedAtUtc = item.CreatedAtUtc; item.ReviewNote = "Check destination";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Url(id), Input)).StatusCode);
        await LoginAsync(client, "other@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(first.Headers.Location)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Contract_creation_and_refund_request_are_mutually_exclusive(bool refundFirst)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await SeedAsync(factory);
        async Task<HttpStatusCode> Refund() { await LoginAsync(client, "customer@example.test"); return (await client.PostAsJsonAsync(Url(id), Input)).StatusCode; }
        async Task<HttpStatusCode> Contract()
        {
            await LoginAsync(client, "manager@example.test");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
            return (await client.PostAsJsonAsync("/api/contracts", new { bookingId = id, startDate = today, endDate = today.AddMonths(6) })).StatusCode;
        }
        Assert.Equal(HttpStatusCode.Created, refundFirst ? await Refund() : await Contract());
        Assert.Equal(HttpStatusCode.Conflict, refundFirst ? await Contract() : await Refund());
    }

    [Fact]
    public async Task Missing_booking_returns_not_found()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Url(999999), Input)).StatusCode);
    }

    [Phase31BPostgreSqlFact]
    public async Task PostgreSQL_serializes_duplicate_claims_and_contract_refund_race_under_shared_locks()
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION"))
            { Pooling = false, Timeout = 5, CommandTimeout = 30 };
        Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" });
        Assert.Equal(55432, server.Port);
        Assert.Equal("postgres", server.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", server.ApplicationName);
        await using var admin = new NpgsqlConnection(server.ConnectionString);
        await admin.OpenAsync();
        Assert.StartsWith("17.", admin.PostgreSqlVersion.ToString());
        var databaseName = "prn232_refund_p6_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var database = new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = databaseName };
            using var factory = new BookingsApiFactory();
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["PayOS:Enabled"] = "false", ["PayOSPayout:Enabled"] = "false" }))
                .ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(options => options.UseNpgsql(database.ConnectionString));
                }));
            using var client = app.CreateClient();
            using (var pgScope = app.Services.CreateScope())
            {
                var pg = pgScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await pg.Database.EnsureCreatedAsync();
                if (!await pg.Roles.AnyAsync())
                {
                    // The parent factory seeds its SQLite connection in ConfigureClient.
                    // Copy only reference fixtures into this uniquely named PostgreSQL DB.
                    using var originalScope = factory.Services.CreateScope();
                    var original = originalScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    pg.AddRange(await original.Roles.AsNoTracking().ToListAsync());
                    pg.AddRange(await original.Users.AsNoTracking().ToListAsync());
                    pg.AddRange(await original.Properties.AsNoTracking().ToListAsync());
                    pg.AddRange(await original.RoomTypes.AsNoTracking().ToListAsync());
                    pg.AddRange(await original.Rooms.AsNoTracking().ToListAsync());
                    pg.AddRange(await original.PropertyManagerAssignments.AsNoTracking().ToListAsync());
                    await pg.SaveChangesAsync();
                }
            }
            var id = await SeedAsync(app);
            await LoginAsync(client, "customer@example.test");
            var duplicates = await Task.WhenAll(client.PostAsJsonAsync(Url(id), Input), client.PostAsJsonAsync(Url(id), Input));
            Assert.Single(duplicates, item => item.StatusCode == HttpStatusCode.Created);
            Assert.Single(duplicates, item => item.StatusCode == HttpStatusCode.Conflict);

            // P8: distinct PostgreSQL contexts/process-equivalent workers race for the same approved claim.
            using (var query = app.Services.CreateScope())
            {
                var pending = await query.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefundRequests.SingleAsync();
                await LoginAsync(client, "admin@example.test");
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/booking-refund-requests/{pending.BookingRefundRequestId}/review",
                    new ReviewBookingRefundDto(true, 3000000, "PostgreSQL payout race"))).StatusCode);
            }
            var payout = new BookingRefundPayoutTests.Gateway();
            await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => BookingRefundPayoutTests.RunAsync(app.Services, payout)));
            Assert.Equal(1, payout.Submits);
            using (var query = app.Services.CreateScope())
            {
                var attempt = await query.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync();
                Assert.Equal("Processing", attempt.Status);
                Assert.True(Guid.TryParseExact(attempt.IdempotencyKey, "N", out _));
            }

            var other = await SeedAsync(app, roomId: 2);
            await LoginAsync(client, "customer@example.test");
            var customerToken = client.DefaultRequestHeaders.Authorization;
            await LoginAsync(client, "manager@example.test");
            var managerToken = client.DefaultRequestHeaders.Authorization;
            client.DefaultRequestHeaders.Authorization = null;
            using var refundRequest = new HttpRequestMessage(HttpMethod.Post, Url(other)) { Content = JsonContent.Create(Input) };
            refundRequest.Headers.Authorization = customerToken;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
            using var contractRequest = new HttpRequestMessage(HttpMethod.Post, "/api/contracts")
            { Content = JsonContent.Create(new { bookingId = other, startDate = today, endDate = today.AddMonths(6) }) };
            contractRequest.Headers.Authorization = managerToken;
            var raced = await Task.WhenAll(client.SendAsync(refundRequest), client.SendAsync(contractRequest));
            Assert.Single(raced, item => item.StatusCode == HttpStatusCode.Created);
            Assert.Single(raced, item => item.StatusCode == HttpStatusCode.Conflict);
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.BookingRefundRequests.CountAsync(item => item.BookingId == other) +
                await db.Contracts.CountAsync(item => item.BookingId == other));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
