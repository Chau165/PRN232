using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingPaymentRequestTargetTests
{
    private static PaymentRequests Request(int? invoiceId, int? bookingId, long order = 123, string status = "Creating") => new()
    {
        InvoiceId = invoiceId, BookingId = bookingId, CustomerId = 10, Amount = 1000, OrderCode = order,
        Status = status, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5), UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
    };

    private static async Task<int> AddBookingAsync(AppDbContext db)
    {
        var booking = new Bookings { RoomId = 1, CustomerId = 10, DepositAmount = 1000,
            Status = "Pending", ExpiryDate = DateTime.UtcNow.AddMinutes(15) };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.BookingId;
    }

    [Fact]
    public async Task Booking_target_round_trips_without_an_invoice_or_money_received()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await AddBookingAsync(db);
        db.PaymentRequests.Add(Request(null, id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var request = await db.PaymentRequests.Include(item => item.Booking).Include(item => item.Customer).SingleAsync();
        Assert.Null(request.InvoiceId);
        Assert.Null(request.Invoice);
        Assert.Equal(id, request.BookingId);
        Assert.Equal(id, request.Booking!.BookingId);
        Assert.Equal("Pending", request.Booking.Status);
        Assert.Equal(10, request.Customer!.UserId);
        Assert.Null(request.PaymentId);
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal(request.PaymentRequestId, (await db.Bookings.Include(item => item.PaymentRequests).SingleAsync()).PaymentRequests.Single().PaymentRequestId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_rejects_both_missing_and_both_present_targets(bool bothPresent)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = (await db.Bookings.SingleAsync()).BookingId;
        db.PaymentRequests.Add(Request(bothPresent ? invoiceId : null, bothPresent ? bookingId : null));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Booking_foreign_key_rejects_a_nonexistent_booking()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PaymentRequests.Add(Request(null, 99999));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("Creating")]
    [InlineData("Pending")]
    [InlineData("RequiresReview")]
    public async Task Each_booking_can_have_only_one_active_or_uncertain_request(string status)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = await AddBookingAsync(db);
        db.PaymentRequests.Add(Request(null, bookingId, 123, status));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.PaymentRequests.Add(Request(null, bookingId, 124));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Historical_booking_attempts_and_separate_invoice_and_booking_active_slots_are_allowed()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = (await db.Bookings.SingleAsync()).BookingId;
        db.PaymentRequests.AddRange(Request(invoiceId, null, 100), Request(null, bookingId, 101),
            Request(null, bookingId, 102, "Cancelled"), Request(null, bookingId, 103, "Expired"),
            Request(null, bookingId, 104, "Create-failed"), Request(null, bookingId, 105, "Succeeded"));
        await db.SaveChangesAsync();
        Assert.Equal(6, await db.PaymentRequests.CountAsync());
        Assert.Equal(1, await db.PaymentRequests.CountAsync(item => item.InvoiceId != null));
        Assert.Equal(5, await db.PaymentRequests.CountAsync(item => item.BookingId != null));
    }

    [Theory]
    [InlineData("order")]
    [InlineData("link")]
    [InlineData("payment")]
    public async Task Provider_identifiers_and_settlement_links_remain_unique_across_target_types(string duplicate)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = (await db.Bookings.SingleAsync()).BookingId;
        var paymentId = (await db.Payments.SingleAsync()).PaymentId;
        var first = Request(invoiceId, null, 100, "Create-failed");
        first.PaymentLinkId = "cross-target-link";
        if (duplicate == "payment") first.PaymentId = paymentId;
        db.PaymentRequests.Add(first);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var second = Request(null, bookingId, duplicate == "order" ? 100 : 101);
        if (duplicate == "link") second.PaymentLinkId = first.PaymentLinkId;
        if (duplicate == "payment") second.PaymentId = paymentId;
        db.PaymentRequests.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_a_booking_with_request_history_is_restricted_by_the_database()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = await AddBookingAsync(db);
        db.PaymentRequests.Add(Request(null, bookingId, 100, "Cancelled"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => db.Bookings.Where(item => item.BookingId == bookingId).ExecuteDeleteAsync());
        Assert.True(await db.Bookings.AnyAsync(item => item.BookingId == bookingId));
    }

    [Fact]
    public async Task Invoice_only_read_update_and_reconciliation_paths_do_not_process_booking_requests_in_P2()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = await AddBookingAsync(db);
        var request = Request(null, bookingId);
        db.PaymentRequests.Add(request);
        await db.SaveChangesAsync();
        var statuses = scope.ServiceProvider.GetRequiredService<IPaymentRequestStatusRepository>();
        Assert.Null(await statuses.GetByIdAsync(request.PaymentRequestId, 10, null, default));
        Assert.False(await statuses.ExistsAsync(request.PaymentRequestId, default));
        var reconciliation = scope.ServiceProvider.GetRequiredService<IPaymentReconciliationRepository>();
        Assert.Null(await reconciliation.GetAsync(request.PaymentRequestId, default));
        Assert.Empty(await reconciliation.FindDueAsync(DateTime.UtcNow, 10, default));
        await Assert.ThrowsAsync<InvoiceStatusLinkConflictException>(() => reconciliation.ApplyAsync(request.PaymentRequestId, null, null, null, DateTime.UtcNow, default));
        await Assert.ThrowsAsync<InvoiceStatusLinkConflictException>(() => scope.ServiceProvider.GetRequiredService<IInvoicePaymentLinkRepository>()
            .UpdateAsync(request.PaymentRequestId, "Creating", "Pending", "book-link", "https://pay.payos.vn/web/book-link", null, DateTime.UtcNow, default));
        Assert.Equal(0, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Known_booking_webhook_is_retained_for_review_without_using_invoice_settlement()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingId = await AddBookingAsync(db);
        var request = Request(null, bookingId);
        request.PaymentLinkId = "book-link";
        db.PaymentRequests.Add(request);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IPayOSWebhookRepository>().RecordAsync(new PaymentWebhookReceipts
        {
            OrderCode = 123, PaymentLinkId = "book-link", Reference = "book-ref", Amount = 1000, Currency = "VND", DataCode = "00",
            TransactionDateTime = DateTime.UtcNow.ToString("O"), ReceivedAtUtc = DateTime.UtcNow, Fingerprint = "test-booking-fingerprint"
        }, true, default);
        var receipt = await db.PaymentWebhookReceipts.SingleAsync();
        Assert.Equal(request.PaymentRequestId, receipt.PaymentRequestId);
        Assert.Equal("RequiresReview", receipt.Outcome);
        Assert.Equal("BOOKING_CHECKOUT_NOT_SUPPORTED", request.LastErrorCode);
        Assert.Equal("RequiresReview", request.Status);
        Assert.Equal("Pending", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal(0, await db.PaymentReviewCases.CountAsync());
    }

    [Fact]
    public void PostgreSql_upgrade_script_adds_the_booking_target_without_deleting_legacy_invoice_requests()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var migration = db.Database.GetMigrations().Last();
        Assert.EndsWith("_AddBookingPaymentRequestTarget", migration);
        var sql = db.GetService<IMigrator>().GenerateScript("20261111014953_AddPaymentReviewCases", migration);
        Assert.Contains("CK_PaymentRequests_ExactlyOneTarget", sql);
        Assert.Contains("UX_PaymentRequests_BookingId_Active", sql);
        Assert.Contains("\"InvoiceId\" DROP NOT NULL", sql);
        Assert.Contains("FOREIGN KEY (\"BookingId\") REFERENCES \"Bookings\" (\"BookingId\") ON DELETE RESTRICT", sql);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN \"InvoiceId\"", sql, StringComparison.OrdinalIgnoreCase);
    }
}
