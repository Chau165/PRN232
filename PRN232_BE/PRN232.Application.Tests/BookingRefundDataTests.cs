using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingRefundDataTests
{
    private static readonly DateTime At = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
    private static async Task<(int BookingId, int PaymentId)> DepositAsync(AppDbContext db, int roomId = 1)
    {
        var booking = new Bookings { RoomId = roomId, CustomerId = 10, DepositAmount = 3000000, Status = "Deposited", ExpiryDate = At.AddDays(-1) };
        var payment = new Payments { Booking = booking, UserId = 10, Amount = 3000000, Status = "Success", PaymentMethod = "PayOS", PaidAtUtc = At.AddDays(-1) };
        db.Payments.Add(payment); await db.SaveChangesAsync();
        return (booking.BookingId, payment.PaymentId);
    }

    private static BookingRefundRequests Refund(int booking, int payment, string status = "PendingReview")
    {
        var item = new BookingRefundRequests { BookingId = booking, OriginalPaymentId = payment, CustomerId = 10,
            RequestedAmount = 3000000, Reason = "Customer requests cancellation", RecipientBankBin = "970422",
            RecipientAccountNumber = "0000000000", CreatedAtUtc = At, UpdatedAtUtc = At.AddMinutes(5), Status = status };
        if (status != "PendingReview")
        {
            item.ReviewedByAdminId = 9; item.ReviewedAtUtc = At.AddMinutes(1); item.RecipientAccountName = "TEST CUSTOMER";
            if (status == "Rejected") item.ReviewNote = "Rejected after checking booking";
            else item.ApprovedAmount = 3000000;
            if (status == "Refunded") item.RefundedAtUtc = At.AddMinutes(4);
        }
        return item;
    }

    private static PayoutAttempts Attempt(int refundId, int number = 1, string status = "Creating")
    {
        var id = Guid.NewGuid().ToString("N");
        var item = new PayoutAttempts { BookingRefundRequestId = refundId, AttemptNumber = number, InitiatedByAdminId = 9,
            ReferenceId = "refund-" + id, IdempotencyKey = id, Amount = 3000000, RecipientBankBin = "970422",
            RecipientAccountNumber = "0000000000", RecipientAccountName = "TEST CUSTOMER", Description = "HOAN COC",
            CreatedAtUtc = At.AddMinutes(2), UpdatedAtUtc = At.AddMinutes(5), Status = status };
        if (status is "Processing" or "Succeeded" or "Transfer-failed")
        { item.ProviderPayoutId = "payout-" + id; item.SubmittedAtUtc = At.AddMinutes(3); }
        if (status == "Succeeded") item.ProviderTransactionId = "txn-" + id;
        if (status is "Succeeded" or "Create-failed" or "Transfer-failed") item.CompletedAtUtc = At.AddMinutes(4);
        if (status is "Create-failed" or "Transfer-failed") item.LastErrorCode = "TEST_REJECTION";
        return item;
    }

    private static async Task<BookingRefundRequests> AddRefundAsync(AppDbContext db, string status = "Refunding", int roomId = 1)
    {
        var deposit = await DepositAsync(db, roomId); var refund = Refund(deposit.BookingId, deposit.PaymentId, status);
        db.BookingRefundRequests.Add(refund); await db.SaveChangesAsync(); return refund;
    }

    [Fact]
    public async Task Pending_request_and_creating_attempt_preserve_original_payment_booking_and_audit_relations()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deposit = await DepositAsync(db); var refund = Refund(deposit.BookingId, deposit.PaymentId);
        Assert.Equal(BookingRefundRequestStatuses.PendingReview, new BookingRefundRequests().Status);
        Assert.Equal(PayoutAttemptStatuses.Creating, new PayoutAttempts().Status);
        db.BookingRefundRequests.Add(refund); await db.SaveChangesAsync();
        var approved = Refund(deposit.BookingId, deposit.PaymentId, "Refunding");
        refund.Status = approved.Status; refund.ApprovedAmount = approved.ApprovedAmount;
        refund.ReviewedByAdminId = approved.ReviewedByAdminId; refund.ReviewedAtUtc = approved.ReviewedAtUtc;
        refund.RecipientAccountName = approved.RecipientAccountName;
        var attempt = Attempt(refund.BookingRefundRequestId); db.PayoutAttempts.Add(attempt); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.BookingRefundRequests.Include(item => item.Booking).Include(item => item.Customer)
            .Include(item => item.OriginalPayment).Include(item => item.ReviewedByAdmin).Include(item => item.PayoutAttempts).SingleAsync();
        Assert.Equal("Deposited", stored.Booking!.Status); Assert.Equal("Success", stored.OriginalPayment!.Status);
        Assert.Equal(3000000, stored.OriginalPayment.Amount); Assert.Equal(At.AddDays(-1), stored.OriginalPayment.PaidAtUtc);
        Assert.Equal(10, stored.Customer!.UserId); Assert.Equal(9, stored.ReviewedByAdmin!.UserId);
        var payout = Assert.Single(stored.PayoutAttempts); Assert.Equal("Creating", payout.Status);
        Assert.Null(payout.ProviderPayoutId); Assert.Null(payout.ProviderTransactionId); Assert.Null(payout.CompletedAtUtc);
        Assert.Equal(1, await db.Payments.CountAsync()); Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Theory]
    [InlineData("PendingReview")]
    [InlineData("Approved")]
    [InlineData("Refunding")]
    [InlineData("RequiresReview")]
    [InlineData("Refunded")]
    public async Task An_active_or_refunded_request_claims_booking_and_payment_against_another_request(string status)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var first = await AddRefundAsync(db, status);
        db.ChangeTracker.Clear(); db.BookingRefundRequests.Add(Refund(first.BookingId, first.OriginalPaymentId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("payment")]
    public async Task Booking_and_original_payment_claims_are_independently_unique(string duplicate)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var first = await AddRefundAsync(db);
        var secondDeposit = await DepositAsync(db, 2);
        db.BookingRefundRequests.Add(Refund(duplicate == "booking" ? first.BookingId : secondDeposit.BookingId,
            duplicate == "payment" ? first.OriginalPaymentId : secondDeposit.PaymentId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Rejection_keeps_history_and_allows_a_new_request_for_the_same_deposit()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var first = await AddRefundAsync(db, "Rejected");
        db.BookingRefundRequests.Add(Refund(first.BookingId, first.OriginalPaymentId)); await db.SaveChangesAsync();
        Assert.Equal(2, await db.BookingRefundRequests.CountAsync());
    }

    [Fact]
    public async Task Monetary_checks_compare_numeric_values_instead_of_provider_specific_decimal_text_ordering()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var deposit = await DepositAsync(db);
        var refund = Refund(deposit.BookingId, deposit.PaymentId, "Approved"); refund.ApprovedAmount = 900000.00m;
        db.BookingRefundRequests.Add(refund); await db.SaveChangesAsync();
        Assert.Equal(900000m, (await db.BookingRefundRequests.AsNoTracking().SingleAsync()).ApprovedAmount);
    }

    [Theory]
    [InlineData("Creating")]
    [InlineData("Processing")]
    [InlineData("RequiresReview")]
    [InlineData("Succeeded")]
    public async Task Active_uncertain_or_successful_payout_prevents_a_second_transfer(string status)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        db.PayoutAttempts.Add(Attempt(refund.BookingRefundRequestId, 1, status)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.PayoutAttempts.Add(Attempt(refund.BookingRefundRequestId, 2)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Verified_failure_history_allows_new_attempt_keys_and_destination_snapshots()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        var first = Attempt(refund.BookingRefundRequestId, 1, "Create-failed");
        var second = Attempt(refund.BookingRefundRequestId, 2, "Transfer-failed"); var third = Attempt(refund.BookingRefundRequestId, 3);
        db.PayoutAttempts.AddRange(first, second, third); await db.SaveChangesAsync();
        refund.RecipientAccountNumber = "1111111111"; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await db.PayoutAttempts.Include(item => item.InitiatedByAdmin).OrderBy(item => item.AttemptNumber).ToListAsync();
        Assert.Equal(3, stored.Count); Assert.Equal(3, stored.Select(item => item.IdempotencyKey).Distinct().Count());
        Assert.All(stored, item => Assert.Equal("0000000000", item.RecipientAccountNumber));
        Assert.All(stored, item => Assert.Equal(9, item.InitiatedByAdmin!.UserId));
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("idempotency")]
    [InlineData("provider-payout")]
    [InlineData("provider-transaction")]
    public async Task Provider_and_application_keys_are_unique_across_refunds(string duplicate)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var firstRefund = await AddRefundAsync(db);
        var secondRefund = await AddRefundAsync(db, roomId: 2);
        var first = Attempt(firstRefund.BookingRefundRequestId, status: "Succeeded");
        var second = Attempt(secondRefund.BookingRefundRequestId, status: "Succeeded");
        if (duplicate == "reference") second.ReferenceId = first.ReferenceId;
        if (duplicate == "idempotency") second.IdempotencyKey = first.IdempotencyKey;
        if (duplicate == "provider-payout") second.ProviderPayoutId = first.ProviderPayoutId;
        if (duplicate == "provider-transaction") second.ProviderTransactionId = first.ProviderTransactionId;
        db.PayoutAttempts.Add(first); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); db.PayoutAttempts.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Attempt_numbers_cannot_be_reused_even_after_failure()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        db.PayoutAttempts.Add(Attempt(refund.BookingRefundRequestId, 1, "Create-failed")); await db.SaveChangesAsync();
        db.PayoutAttempts.Add(Attempt(refund.BookingRefundRequestId, 1)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("amount-zero")]
    [InlineData("fraction")]
    [InlineData("over-approved")]
    [InlineData("approved-null")]
    [InlineData("unknown-status")]
    [InlineData("currency")]
    [InlineData("bank")]
    [InlineData("account")]
    [InlineData("reason")]
    [InlineData("updated-time")]
    [InlineData("review-time")]
    [InlineData("reviewer")]
    [InlineData("recipient-name")]
    [InlineData("refunded-time")]
    [InlineData("rejection-note")]
    public async Task Refund_checks_reject_invalid_money_state_destination_and_time(string invalid)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var deposit = await DepositAsync(db);
        var refund = Refund(deposit.BookingId, deposit.PaymentId, invalid == "refunded-time" ? "Refunded" : invalid == "rejection-note" ? "Rejected" : "Approved");
        switch (invalid)
        {
            case "amount-zero": refund.RequestedAmount = 0; break;
            case "fraction": refund.RequestedAmount = 3000000.5m; break;
            case "over-approved": refund.ApprovedAmount = refund.RequestedAmount + 1; break;
            case "approved-null": refund.ApprovedAmount = null; break;
            case "unknown-status": refund.Status = "Failed"; break;
            case "currency": refund.Currency = "USD"; break;
            case "bank": refund.RecipientBankBin = " "; break;
            case "account": refund.RecipientAccountNumber = " "; break;
            case "reason": refund.Reason = " "; break;
            case "updated-time": refund.UpdatedAtUtc = At.AddSeconds(-1); break;
            case "review-time": refund.ReviewedAtUtc = At.AddMinutes(6); break;
            case "reviewer": refund.ReviewedByAdminId = null; break;
            case "recipient-name": refund.RecipientAccountName = null; break;
            case "refunded-time": refund.RefundedAtUtc = null; break;
            case "rejection-note": refund.ReviewNote = null; break;
        }
        db.BookingRefundRequests.Add(refund); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("amount-zero")]
    [InlineData("fraction")]
    [InlineData("fee")]
    [InlineData("currency")]
    [InlineData("provider")]
    [InlineData("number")]
    [InlineData("reference")]
    [InlineData("idempotency")]
    [InlineData("bank")]
    [InlineData("account")]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("unknown-status")]
    [InlineData("completed-time")]
    [InlineData("complete-before-submit")]
    [InlineData("updated-time")]
    [InlineData("submitted-time")]
    [InlineData("checked-time")]
    [InlineData("provider-id")]
    [InlineData("provider-txn")]
    [InlineData("blank-provider-id")]
    [InlineData("failed-code")]
    public async Task Payout_checks_reject_invalid_money_identity_state_and_time(string invalid)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        var attempt = Attempt(refund.BookingRefundRequestId, status: invalid == "failed-code" ? "Transfer-failed" : "Succeeded");
        switch (invalid)
        {
            case "amount-zero": attempt.Amount = 0; break;
            case "fraction": attempt.Amount = 0.5m; break;
            case "fee": attempt.FeeAmount = -1; break;
            case "currency": attempt.Currency = "USD"; break;
            case "provider": attempt.Provider = "Other"; break;
            case "number": attempt.AttemptNumber = 0; break;
            case "reference": attempt.ReferenceId = " "; break;
            case "idempotency": attempt.IdempotencyKey = " "; break;
            case "bank": attempt.RecipientBankBin = " "; break;
            case "account": attempt.RecipientAccountNumber = " "; break;
            case "name": attempt.RecipientAccountName = " "; break;
            case "description": attempt.Description = " "; break;
            case "unknown-status": attempt.Status = "Failed"; break;
            case "completed-time": attempt.CompletedAtUtc = null; break;
            case "complete-before-submit": attempt.CompletedAtUtc = At.AddMinutes(2); break;
            case "updated-time": attempt.UpdatedAtUtc = At.AddMinutes(1); break;
            case "submitted-time": attempt.SubmittedAtUtc = null; break;
            case "checked-time": attempt.LastCheckedAtUtc = At.AddMinutes(6); break;
            case "provider-id": attempt.ProviderPayoutId = null; break;
            case "provider-txn": attempt.ProviderTransactionId = null; break;
            case "blank-provider-id": attempt.ProviderPayoutId = " "; break;
            case "failed-code": attempt.LastErrorCode = null; break;
        }
        db.PayoutAttempts.Add(attempt); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Transfer_time_can_remain_unknown_and_is_not_filled_from_local_confirmation_time()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        var attempt = Attempt(refund.BookingRefundRequestId, status: "Succeeded"); db.PayoutAttempts.Add(attempt); await db.SaveChangesAsync();
        Assert.NotNull(attempt.CompletedAtUtc); Assert.Null(attempt.TransferredAtUtc);
        Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("payment")]
    [InlineData("customer")]
    [InlineData("reviewer")]
    [InlineData("payout-parent")]
    [InlineData("payout-admin")]
    public async Task Foreign_keys_reject_missing_audit_or_money_source_records(string invalid)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var deposit = await DepositAsync(db);
        var refund = Refund(deposit.BookingId, deposit.PaymentId, "Approved");
        if (invalid == "booking") refund.BookingId = 99999;
        if (invalid == "payment") refund.OriginalPaymentId = 99999;
        if (invalid == "customer") refund.CustomerId = 99999;
        if (invalid == "reviewer") refund.ReviewedByAdminId = 99999;
        db.BookingRefundRequests.Add(refund);
        if (invalid.StartsWith("payout-", StringComparison.Ordinal))
        {
            await db.SaveChangesAsync(); var attempt = Attempt(refund.BookingRefundRequestId);
            if (invalid == "payout-parent") attempt.BookingRefundRequestId = 99999; else attempt.InitiatedByAdminId = 99999;
            db.PayoutAttempts.Add(attempt);
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Historical_refunds_and_payouts_restrict_deletion_of_source_payment_request_and_admin()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var refund = await AddRefundAsync(db);
        db.PayoutAttempts.Add(Attempt(refund.BookingRefundRequestId, status: "Create-failed")); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => db.Payments.Where(item => item.PaymentId == refund.OriginalPaymentId).ExecuteDeleteAsync());
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => db.BookingRefundRequests.Where(item => item.BookingRefundRequestId == refund.BookingRefundRequestId).ExecuteDeleteAsync());
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => db.Users.Where(item => item.UserId == 9).ExecuteDeleteAsync());
        Assert.Equal(1, await db.BookingRefundRequests.CountAsync()); Assert.Equal(1, await db.PayoutAttempts.CountAsync());
    }

    [Fact]
    public void PostgreSql_migration_adds_refund_tables_without_rewriting_incoming_payment_history()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migration = db.Database.GetMigrations().Single(item => item.EndsWith("_AddBookingRefundRequestsAndPayoutAttempts", StringComparison.Ordinal));
        var sql = db.GetService<IMigrator>().GenerateScript("20261111152245_AddBookingPaymentRequestTarget", migration);
        Assert.Contains("CREATE TABLE \"BookingRefundRequests\"", sql); Assert.Contains("CREATE TABLE \"PayoutAttempts\"", sql);
        Assert.Contains("UX_PayoutAttempts_RefundRequestId_Claimed", sql); Assert.Contains("'Succeeded'", sql);
        Assert.Contains("UX_BookingRefundRequests_OriginalPaymentId_Claimed", sql); Assert.Contains("'Refunded'", sql);
        Assert.Contains("ON DELETE RESTRICT", sql); Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE \"Payments\"", sql, StringComparison.OrdinalIgnoreCase);
    }
}
