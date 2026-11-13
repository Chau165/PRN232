using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class BookingRefundCompletionTests
{
    private static readonly PayOSPayoutSettings Enabled = new() { Enabled = true, ReconciliationEnabled = true };
    private static async Task<PayoutAttempts> SeedAsync(BookingsApiFactory factory, HttpClient client, bool succeeded = false)
    {
        var id = await BookingRefundPayoutTests.ApproveAsync(factory, client);
        using var scope = factory.Services.CreateScope();
        var attempt = (await scope.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>().TryClaimAsync(id, DateTime.UtcNow, default))!;
        if (succeeded)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            attempt.Status = "Succeeded"; attempt.ProviderPayoutId = "completion-payout-" + attempt.PayoutAttemptId; attempt.ProviderTransactionId = "completion-transaction-" + attempt.PayoutAttemptId;
            attempt.ProviderApprovalState = "Completed"; attempt.ProviderTransactionState = "Succeeded";
            attempt.SubmittedAtUtc = attempt.CreatedAtUtc; attempt.CompletedAtUtc = DateTime.UtcNow; attempt.UpdatedAtUtc = attempt.CompletedAtUtc.Value;
            await db.SaveChangesAsync();
        }
        return attempt;
    }
    private static async Task ReconcileAsync(IServiceProvider services, PayoutAttempts attempt, PayoutReconciliationTests.Clock clock)
    {
        using var scope = services.CreateScope();
        var gateway = new PayoutReconciliationTests.Gateway { Response = new PayoutRemoteSnapshot("completion-payout", attempt.ReferenceId,
            "completion-transaction", attempt.ReferenceId, attempt.Amount, attempt.RecipientBankBin, attempt.RecipientAccountNumber, "Completed", "Succeeded",
            "BANK-COMPLETION", clock.Now.AddMinutes(-1).ToString("O")) };
        await new PayoutReconciliationService(scope.ServiceProvider.GetRequiredService<IPayoutReconciliationRepository>(), gateway, Enabled, clock).RunBatchAsync(default);
    }
    private static async Task<bool> CompleteAsync(IServiceProvider services, int attemptId, DateTime now)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBookingRefundCompletionRepository>().CompleteAsync(attemptId, now, default);
    }

    [Fact]
    public async Task Reconciliation_records_actual_refund_and_cancels_booking_with_notifications_once()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new PayoutReconciliationTests.Clock();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Rooms.Where(RoomRentabilityPolicy.IsRentableAt(new DateTimeOffset(clock.Now))).AnyAsync(item => item.RoomId == 1));
        }
        await ReconcileAsync(factory.Services, attempt, clock);
        Assert.True(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, clock.Now.AddMinutes(1)));
        await ReconcileAsync(factory.Services, attempt, clock);
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var refund = await context.BookingRefunds.SingleAsync(); var request = await context.BookingRefundRequests.SingleAsync();
        var booking = await context.Bookings.SingleAsync();
        Assert.Equal((attempt.PayoutAttemptId, request.OriginalPaymentId, booking.BookingId, 10, 2500000m, "VND"),
            (refund.PayoutAttemptId, refund.OriginalPaymentId, refund.BookingId, refund.CustomerId, refund.Amount, refund.Currency));
        Assert.Equal(clock.Now, refund.RecordedAtUtc); Assert.Equal(clock.Now.AddMinutes(-1), refund.TransferredAtUtc);
        Assert.Equal("Refunded", request.Status); Assert.Equal(refund.RecordedAtUtc, request.RefundedAtUtc);
        Assert.Equal("Cancelled", booking.Status);
        Assert.True(await context.Rooms.Where(RoomRentabilityPolicy.IsRentableAt(new DateTimeOffset(clock.Now))).AnyAsync(item => item.RoomId == booking.RoomId));
        Assert.Equal("Success", (await context.Payments.SingleAsync()).Status); Assert.Equal(3000000m, (await context.Payments.SingleAsync()).Amount);
        Assert.Equal("Succeeded", (await context.PaymentRequests.SingleAsync()).Status);
        var title = $"Đã hoàn cọc và hủy booking #{booking.BookingId}";
        Assert.Equal(new[] { 7, 9, 10, 11 }, await context.Notifications.Where(item => item.Title == title).OrderBy(item => item.UserId).Select(item => item.UserId).ToArrayAsync());
        await LoginAsync(client, "customer@example.test");
        var dto = await client.GetFromJsonAsync<BookingRefundDto>($"/api/bookings/{booking.BookingId}/refund-requests/{request.BookingRefundRequestId}");
        Assert.Equal("Refunded", dto!.Status); Assert.Equal(DateTimeKind.Utc, dto.RefundedAtUtc!.Value.Kind);
        Assert.Equal("****7890", dto.MaskedRecipientAccountNumber);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_in_notification_insert_rolls_back_payout_confirmation_ledger_request_booking_and_room(bool previouslyConfirmed)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, previouslyConfirmed);
        var clock = new PayoutReconciliationTests.Clock();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rooms.SingleAsync(item => item.RoomId == 1)).Status = "Reserved"; await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_refund_notice BEFORE INSERT ON Notifications WHEN NEW.Title LIKE 'Đã hoàn cọc%' BEGIN SELECT RAISE(ABORT, 'test notification failure'); END;");
        }
        async Task FinalizeAsync()
        {
            if (previouslyConfirmed) await CompleteAsync(factory.Services, attempt.PayoutAttemptId, clock.Now);
            else await ReconcileAsync(factory.Services, attempt, clock);
        }
        await Assert.ThrowsAsync<DbUpdateException>(FinalizeAsync);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(previouslyConfirmed ? "Succeeded" : "Creating", (await db.PayoutAttempts.SingleAsync()).Status); Assert.Empty(await db.BookingRefunds.ToListAsync());
            Assert.Equal("Refunding", (await db.BookingRefundRequests.SingleAsync()).Status); Assert.Null((await db.BookingRefundRequests.SingleAsync()).RefundedAtUtc);
            Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status); Assert.Equal("Reserved", (await db.Rooms.SingleAsync(item => item.RoomId == 1)).Status);
            Assert.Empty(await db.Notifications.Where(item => item.Title.StartsWith("Đã hoàn cọc")).ToListAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_refund_notice;");
        }
        clock.Now = clock.Now.AddMinutes(2); await FinalizeAsync();
        using var check = factory.Services.CreateScope(); Assert.Single(await check.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefunds.ToListAsync());
    }

    [Fact]
    public async Task Recovery_worker_finalizes_preexisting_success_and_preserves_unknown_bank_time()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await SeedAsync(factory, client, true);
        var clock = new PayoutReconciliationTests.Clock();
        using (var scope = factory.Services.CreateScope())
        {
            var service = new BookingRefundCompletionService(scope.ServiceProvider.GetRequiredService<IBookingRefundCompletionRepository>(), Enabled, clock);
            await service.RunBatchAsync(default); await service.RunBatchAsync(default);
        }
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var refund = await db.BookingRefunds.SingleAsync(); Assert.Null(refund.TransferredAtUtc); Assert.Equal("Cancelled", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal("Refunded", (await db.BookingRefundRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("Reserved", false, "Available", true)]
    [InlineData("Reserved", true, "Reserved", false)]
    [InlineData("Available", true, "Available", false)]
    [InlineData("Maintenance", false, "Maintenance", false)]
    [InlineData("Occupied", false, "Occupied", false)]
    public async Task Releasing_a_booking_preserves_other_holds_and_non_rentable_room_states(string initial, bool otherHold, string expected, bool rentable)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        var clock = new PayoutReconciliationTests.Clock();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rooms.SingleAsync(item => item.RoomId == 1)).Status = initial;
            if (otherHold) db.Bookings.Add(new Bookings { RoomId = 1, CustomerId = 20, DepositAmount = 3000000, Status = "Pending", ExpiryDate = clock.Now.AddMinutes(5) });
            await db.SaveChangesAsync();
        }
        Assert.True(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, clock.Now));
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(expected, (await context.Rooms.SingleAsync(item => item.RoomId == 1)).Status);
        Assert.Equal(rentable, await context.Rooms.Where(RoomRentabilityPolicy.IsRentableAt(new DateTimeOffset(clock.Now))).AnyAsync(item => item.RoomId == 1));
        if (otherHold) Assert.Equal("Pending", (await context.Bookings.SingleAsync(item => item.CustomerId == 20)).Status);
    }

    [Theory]
    [InlineData("contract")]
    [InlineData("terminated-contract")]
    [InlineData("amount")]
    [InlineData("destination")]
    [InlineData("payment")]
    [InlineData("evidence")]
    public async Task Inconsistent_business_facts_keep_successful_bank_evidence_but_do_not_release_booking(string change)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var request = await db.BookingRefundRequests.SingleAsync();
            if (change is "contract" or "terminated-contract") db.Contracts.Add(new Contracts { BookingId = request.BookingId, RoomId = 1, CustomerId = 10,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6), Status = change == "contract" ? "Active" : "Terminated" });
            if (change == "amount") request.ApprovedAmount = 2400000;
            if (change == "destination") request.RecipientAccountNumber = "00999999";
            if (change == "payment") (await db.PaymentRequests.SingleAsync()).Status = "RequiresReview";
            if (change == "evidence") (await db.PayoutAttempts.SingleAsync()).ProviderTransactionState = "Processing";
            await db.SaveChangesAsync();
        }
        Assert.False(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, DateTime.UtcNow.AddMinutes(5)));
        Assert.False(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, DateTime.UtcNow.AddMinutes(6)));
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await context.BookingRefunds.ToListAsync()); Assert.Equal("Succeeded", (await context.PayoutAttempts.SingleAsync()).Status);
        Assert.Equal("Deposited", (await context.Bookings.SingleAsync()).Status); Assert.Equal("RequiresReview", (await context.BookingRefundRequests.SingleAsync()).Status);
        Assert.Equal(2, await context.Notifications.CountAsync(item => item.Title.StartsWith("Hoàn cọc #")));
    }

    [Fact]
    public async Task Current_managers_receive_completion_but_old_and_inactive_managers_do_not()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.ToListAsync());
            db.PropertyManagerAssignments.AddRange(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 }, new PropertyManagerAssignment { PropertyId = 1, ManagerId = 14 });
            await db.SaveChangesAsync();
        }
        Assert.True(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, DateTime.UtcNow.AddMinutes(5)));
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await context.Notifications.Where(item => item.Title.StartsWith("Đã hoàn cọc")).OrderBy(item => item.UserId).Select(item => item.UserId).ToArrayAsync();
        Assert.Equal(new[] { 8, 9, 10, 11 }, users);
    }

    [Fact]
    public async Task Concurrent_completion_creates_only_one_ledger_record_and_one_notice_per_recipient()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CompleteAsync(factory.Services, attempt.PayoutAttemptId, DateTime.UtcNow.AddMinutes(5))));
        Assert.All(results, Assert.True);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.BookingRefunds.ToListAsync()); Assert.Equal(4, await db.Notifications.CountAsync(item => item.Title.StartsWith("Đã hoàn cọc")));
    }

    [Theory]
    [InlineData("Creating")]
    [InlineData("Processing")]
    [InlineData("RequiresReview")]
    [InlineData("Create-failed")]
    [InlineData("Transfer-failed")]
    public async Task Only_confirmed_success_can_finalize_a_booking(string status)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var stored = await db.PayoutAttempts.SingleAsync(); stored.Status = status;
            if (status is "Creating" or "Processing" or "RequiresReview") stored.CompletedAtUtc = null;
            if (status is "Create-failed" or "Transfer-failed") stored.LastErrorCode = "TEST_FAILED";
            if (status == "Create-failed") { stored.ProviderPayoutId = null; stored.ProviderTransactionId = null; stored.SubmittedAtUtc = null; }
            await db.SaveChangesAsync();
        }
        Assert.False(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, DateTime.UtcNow.AddMinutes(5)));
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await context.BookingRefunds.ToListAsync()); Assert.Equal("Deposited", (await context.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("attempt")]
    [InlineData("payment")]
    [InlineData("booking")]
    public async Task Database_unique_keys_prevent_duplicate_refund_records_even_outside_worker(string duplicate)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var first = await SeedAsync(factory, client, true);
        Assert.True(await CompleteAsync(factory.Services, first.PayoutAttemptId, DateTime.UtcNow.AddMinutes(5)));
        var second = await SeedAsync(factory, client, true);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.BookingRefunds.AsNoTracking().SingleAsync();
        var request = await db.BookingRefundRequests.SingleAsync(item => item.BookingRefundRequestId == second.BookingRefundRequestId);
        var extra = new BookingRefunds { BookingRefundRequestId = request.BookingRefundRequestId, PayoutAttemptId = second.PayoutAttemptId,
            OriginalPaymentId = request.OriginalPaymentId, BookingId = request.BookingId, CustomerId = request.CustomerId,
            Amount = second.Amount, RecordedAtUtc = DateTime.UtcNow.AddMinutes(5) };
        if (duplicate == "request") extra.BookingRefundRequestId = original.BookingRefundRequestId;
        if (duplicate == "attempt") extra.PayoutAttemptId = original.PayoutAttemptId;
        if (duplicate == "payment") extra.OriginalPaymentId = original.OriginalPaymentId;
        if (duplicate == "booking") extra.BookingId = original.BookingId;
        db.BookingRefunds.Add(extra); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Migration_adds_append_only_refund_ledger_without_modifying_incoming_payments()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.EndsWith("_AddBookingRefundLedger", migrations[^1]);
        var sql = db.GetService<IMigrator>().GenerateScript(migrations[^2], migrations[^1]);
        Assert.Contains("CREATE TABLE \"BookingRefunds\"", sql); Assert.Contains("ON DELETE RESTRICT", sql);
        foreach (var column in new[] { "BookingRefundRequestId", "PayoutAttemptId", "OriginalPaymentId", "BookingId" })
            Assert.Contains($"CREATE UNIQUE INDEX \"IX_BookingRefunds_{column}\"", sql);
        Assert.Contains("CK_BookingRefunds_Amount", sql); Assert.Contains("CK_BookingRefunds_Times", sql);
        Assert.DoesNotContain("ALTER TABLE \"Payments\"", sql); Assert.DoesNotContain("UPDATE \"Payments\"", sql);
        var rollback = db.GetService<IMigrator>().GenerateScript(migrations[^1], migrations[^2]);
        Assert.Contains("DROP TABLE \"BookingRefunds\"", rollback); Assert.DoesNotContain("DROP TABLE \"Payments\"", rollback);
    }

    [Fact]
    public async Task Monotonic_audit_time_does_not_expire_another_customers_hold_ahead_of_real_clock()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        var now = DateTime.UtcNow.AddMinutes(5); var auditTime = now.AddMinutes(10);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.PayoutAttempts.SingleAsync()).UpdatedAtUtc = auditTime;
            (await db.Rooms.SingleAsync(item => item.RoomId == 1)).Status = "Reserved";
            db.Bookings.Add(new Bookings { RoomId = 1, CustomerId = 20, DepositAmount = 3000000, Status = "Pending", ExpiryDate = now.AddMinutes(2) });
            await db.SaveChangesAsync();
        }
        Assert.True(await CompleteAsync(factory.Services, attempt.PayoutAttemptId, now));
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Reserved", (await context.Rooms.SingleAsync(item => item.RoomId == 1)).Status);
        Assert.Equal(auditTime, (await context.BookingRefunds.SingleAsync()).RecordedAtUtc);
    }
}
