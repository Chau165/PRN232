using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class PayoutReconciliationTests
{
    private static PayOSPayoutSettings Settings(bool retry = false, int maximum = 3) => new()
    { Enabled = true, ReconciliationEnabled = true, RetryFailedPayouts = retry, MaxPayoutAttempts = maximum };

    private static async Task<PayoutAttempts> SeedAsync(BookingsApiFactory factory, HttpClient client, bool processing = false)
    {
        var id = await BookingRefundPayoutTests.ApproveAsync(factory, client);
        if (processing) await BookingRefundPayoutTests.RunAsync(factory.Services, new BookingRefundPayoutTests.Gateway());
        else
        {
            using var claim = factory.Services.CreateScope();
            Assert.NotNull(await claim.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>().TryClaimAsync(id, DateTime.UtcNow, default));
        }
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.AsNoTracking().SingleAsync();
    }

    private static Task RunAsync(IServiceProvider services, Gateway gateway, Clock clock, PayOSPayoutSettings? settings = null, CancellationToken cancellationToken = default) =>
        RunInScopeAsync(services, gateway, clock, settings, cancellationToken);

    // Keep each batch in a separate DbContext, just as the hosted worker does.
    private static async Task RunInScopeAsync(IServiceProvider services, Gateway gateway, Clock clock, PayOSPayoutSettings? settings, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        await new PayoutReconciliationService(scope.ServiceProvider.GetRequiredService<IPayoutReconciliationRepository>(), gateway,
            settings ?? Settings(), clock).RunBatchAsync(cancellationToken);
    }

    private static PayoutRemoteSnapshot Remote(PayoutAttempts attempt, string approval = "Completed", string state = "Succeeded") =>
        new(attempt.ProviderPayoutId ?? "recovered-payout", attempt.ReferenceId, attempt.ProviderTransactionId ?? "recovered-transaction",
            attempt.ReferenceId, attempt.Amount, attempt.RecipientBankBin, attempt.RecipientAccountNumber, approval, state);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovers_original_attempt_using_reference_or_provider_id_and_finalizes_booking_atomically(bool processing)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var original = await SeedAsync(factory, client, processing); var clock = new Clock();
        var actualTime = clock.Now.AddMinutes(-1);
        var gateway = new Gateway { Response = Remote(original) with { BankReference = "BANK-123", TransactionDatetime = actualTime.ToString("O") } };
        await RunAsync(factory.Services, gateway, clock);
        Assert.Equal(new PayoutLookupRequest(original.ReferenceId, original.ProviderPayoutId), gateway.LastRequest);
        clock.Now = clock.Now.AddMinutes(2); await RunAsync(factory.Services, gateway, clock); Assert.Equal(1, gateway.Calls);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.PayoutAttempts.SingleAsync();
        Assert.Equal("Succeeded", attempt.Status); Assert.Equal(original.IdempotencyKey, attempt.IdempotencyKey);
        Assert.Equal("BANK-123", attempt.BankReference); Assert.Equal(actualTime, attempt.TransferredAtUtc); Assert.NotNull(attempt.CompletedAtUtc);
        Assert.Equal("Refunded", (await db.BookingRefundRequests.SingleAsync()).Status);
        Assert.NotNull((await db.BookingRefundRequests.SingleAsync()).RefundedAtUtc);
        Assert.Equal("Cancelled", (await db.Bookings.SingleAsync()).Status); Assert.Equal(1, await db.Payments.CountAsync());
        var refund = await db.BookingRefunds.SingleAsync(); Assert.Equal(original.Amount, refund.Amount);
        Assert.Equal(actualTime, refund.TransferredAtUtc);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("timeout")]
    [InlineData("amount")]
    [InlineData("bank")]
    [InlineData("account")]
    [InlineData("reference")]
    [InlineData("id")]
    [InlineData("time")]
    public async Task Ambiguity_keeps_claim_and_notifies_admins_once_without_authorizing_retry(string failure)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var original = await SeedAsync(factory, client);
        var remote = Remote(original);
        remote = failure switch
        {
            "amount" => remote with { Amount = 1 }, "bank" => remote with { RecipientBankBin = "000000" },
            "account" => remote with { RecipientAccountNumber = "000000" }, "reference" => remote with { ReferenceId = "other" },
            "id" => remote with { ProviderTransactionId = "" }, "time" => remote with { TransactionDatetime = "invalid" }, _ => remote
        };
        var gateway = new Gateway { Response = failure == "missing" ? null : remote, Error = failure == "timeout" ? new TimeoutException("private") : null };
        var clock = new Clock(); await RunAsync(factory.Services, gateway, clock, Settings(true));
        clock.Now = clock.Now.AddMinutes(2); await RunAsync(factory.Services, gateway, clock, Settings(true));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.PayoutAttempts.SingleAsync(); Assert.Equal("RequiresReview", attempt.Status); Assert.Null(attempt.CompletedAtUtc);
        Assert.Null(attempt.LastErrorMessage); Assert.Equal(original.IdempotencyKey, attempt.IdempotencyKey);
        Assert.Equal("RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
        var admins = await db.Users.CountAsync(item => item.IsActive && item.Role!.RoleName == "Admin");
        Assert.Equal(admins, await db.Notifications.CountAsync(item => item.Title == $"Lệnh hoàn cọc #{attempt.PayoutAttemptId} cần kiểm tra"));
        Assert.Single(await db.PayoutAttempts.ToListAsync());
    }

    [Fact]
    public async Task Timeout_can_later_recover_success_and_absent_bank_time_remains_null()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock(); var gateway = new Gateway { Error = new TimeoutException("private") };
        await RunAsync(factory.Services, gateway, clock);
        gateway.Error = null; gateway.Response = Remote(attempt); clock.Now = clock.Now.AddMinutes(2);
        await RunAsync(factory.Services, gateway, clock);
        using var scope = factory.Services.CreateScope(); var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync();
        Assert.Equal("Succeeded", stored.Status); Assert.Null(stored.TransferredAtUtc); Assert.Null(stored.LastErrorCode);
    }

    [Fact]
    public async Task Pending_payout_becomes_stuck_review_and_can_still_recover()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock(); var gateway = new Gateway { Response = Remote(attempt, "Processing", "Processing") };
        await RunAsync(factory.Services, gateway, clock);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal("Processing", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
        clock.Now = clock.Now.AddHours(2); await RunAsync(factory.Services, gateway, clock);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal("PAYOUT_REMOTE_STUCK", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).LastErrorCode);
        clock.Now = clock.Now.AddMinutes(2); gateway.Response = Remote(attempt); await RunAsync(factory.Services, gateway, clock);
        using var check = factory.Services.CreateScope();
        Assert.Equal("Succeeded", (await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false, 3, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 3, true)]
    public async Task Only_verified_failure_with_opt_in_delay_and_attempt_limit_can_reopen_approval(bool retry, int maximum, bool reopened)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock(); var gateway = new Gateway { Response = Remote(attempt, "Failed", "Failed") with { ErrorCode = "DESTINATION_REJECTED" } };
        var settings = Settings(retry, maximum); await RunAsync(factory.Services, gateway, clock, settings);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Transfer-failed", (await db.PayoutAttempts.SingleAsync()).Status);
            Assert.Equal("RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
        }
        clock.Now = clock.Now.AddSeconds(120); await RunAsync(factory.Services, gateway, clock, settings);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal("RequiresReview", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().BookingRefundRequests.SingleAsync()).Status);
        clock.Now = clock.Now.AddMinutes(5); await RunAsync(factory.Services, gateway, clock, settings);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(reopened ? "Approved" : "RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
        }
        if (!reopened) return;
        using var dispatch = factory.Services.CreateScope(); var fake = new BookingRefundPayoutTests.Gateway();
        await new BookingRefundPayoutService(dispatch.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>(), fake, fake,
            new PayOSPayoutSettings { Enabled = true, DispatchEnabled = true }, clock).RunBatchAsync(default);
        using var check = factory.Services.CreateScope();
        var attempts = await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.OrderBy(item => item.AttemptNumber).ToListAsync();
        Assert.Equal(2, attempts.Count); Assert.Equal("Transfer-failed", attempts[0].Status);
        Assert.NotEqual(attempts[0].IdempotencyKey, attempts[1].IdempotencyKey); Assert.Equal(1, fake.Submits);
    }

    [Fact]
    public async Task Failure_that_changes_during_retry_recheck_never_reopens_approval()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock(); var gateway = new Gateway { Response = Remote(attempt, "Failed", "Failed") with { ErrorCode = "FAILED" } };
        await RunAsync(factory.Services, gateway, clock, Settings(true));
        clock.Now = clock.Now.AddMinutes(6); gateway.Response = null;
        await RunAsync(factory.Services, gateway, clock, Settings(true));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
        Assert.Equal("PAYOUT_FAILURE_RECHECK_REQUIRED", (await db.PayoutAttempts.SingleAsync()).LastErrorCode);
    }

    [Fact]
    public async Task Lease_prevents_overlapping_workers_and_old_responses_cannot_overwrite_a_new_check()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new Gateway { Response = Remote(attempt), OnLookup = async () => { entered.TrySetResult(); await resume.Task; } };
        var first = RunAsync(factory.Services, gateway, clock); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { await RunAsync(factory.Services, gateway, clock); Assert.Equal(1, gateway.Calls); }
        finally { resume.TrySetResult(); }
        await first;
        // Terminal success is never polled or downgraded by another batch.
        clock.Now = clock.Now.AddMinutes(2); gateway.Response = null;
        await RunAsync(factory.Services, gateway, clock); Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Disabled_reconciliation_and_recent_attempts_are_not_queried()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        var clock = new Clock { Now = DateTime.UtcNow }; var gateway = new Gateway { Response = Remote(attempt) };
        await RunAsync(factory.Services, gateway, clock); clock.Now = clock.Now.AddMinutes(5);
        await RunAsync(factory.Services, gateway, clock, new PayOSPayoutSettings { Enabled = true });
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Late_response_from_an_expired_lease_cannot_overwrite_a_newer_processing_result()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client, true);
        var clock = new Clock();
        PayoutReconciliationWindow Window() => new(clock.Now, clock.Now.AddMinutes(-1), clock.Now.AddMinutes(-3), clock.Now.AddMinutes(-5), false, 3);
        clock.Now = new DateTime(clock.Now.Ticks - clock.Now.Ticks % 10 + 7, DateTimeKind.Utc);
        var oldWindow = Window();
        using var first = factory.Services.CreateScope(); var oldRepo = first.ServiceProvider.GetRequiredService<IPayoutReconciliationRepository>();
        var oldLease = await oldRepo.TryLeaseAsync(attempt.PayoutAttemptId, oldWindow, default); Assert.NotNull(oldLease);
        Assert.Equal(0, oldLease.LastCheckedAtUtc!.Value.Ticks % 10);
        clock.Now = clock.Now.AddMinutes(2); var newWindow = Window();
        using var second = factory.Services.CreateScope(); var newRepo = second.ServiceProvider.GetRequiredService<IPayoutReconciliationRepository>();
        var newLease = await newRepo.TryLeaseAsync(attempt.PayoutAttemptId, newWindow, default); Assert.NotNull(newLease);
        await newRepo.ApplyAsync(newLease, PayoutReconciliationPolicy.Evaluate(newLease, Remote(attempt, "Processing", "Processing"), clock.Now, 3600), newWindow, clock.Now, default);
        await oldRepo.ApplyAsync(oldLease, PayoutReconciliationPolicy.Evaluate(oldLease, Remote(attempt), clock.Now, 3600), oldWindow, clock.Now, default);
        using var check = factory.Services.CreateScope();
        Assert.Equal("Processing", (await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancellation_during_lookup_preserves_claim_and_a_later_check_can_recover()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var attempt = await SeedAsync(factory, client);
        using var cancellation = new CancellationTokenSource();
        var gateway = new Gateway { OnLookup = () => { cancellation.Cancel(); return Task.CompletedTask; }, Error = new OperationCanceledException(cancellation.Token) };
        var clock = new Clock();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(factory.Services, gateway, clock, cancellationToken: cancellation.Token));
        using (var check = factory.Services.CreateScope())
            Assert.Equal("Creating", (await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
        clock.Now = clock.Now.AddMinutes(2); gateway.Error = null; gateway.OnLookup = null; gateway.Response = Remote(attempt);
        await RunAsync(factory.Services, gateway, clock);
        using var scope = factory.Services.CreateScope();
        Assert.Equal("Succeeded", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Proven_preflight_failure_can_reopen_without_querying_a_provider_that_was_never_called()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await BookingRefundPayoutTests.ApproveAsync(factory, client);
        var submission = new BookingRefundPayoutTests.Gateway { Balance = 1 };
        await BookingRefundPayoutTests.RunAsync(factory.Services, submission);
        var gateway = new Gateway(); var clock = new Clock { Now = DateTime.UtcNow.AddMinutes(10) };
        await RunAsync(factory.Services, gateway, clock, Settings(true));
        Assert.Equal(0, gateway.Calls); Assert.Equal(0, submission.Submits);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Approved", (await db.BookingRefundRequests.SingleAsync()).Status);
        Assert.Equal("Create-failed", (await db.PayoutAttempts.SingleAsync()).Status);
    }

    internal sealed class Clock : TimeProvider
    {
        public DateTime Now = DateTime.UtcNow.AddMinutes(5);
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
    internal sealed class Gateway : IPayOSPayoutReconciliationGateway
    {
        public int Calls;
        public PayoutLookupRequest? LastRequest;
        public PayoutRemoteSnapshot? Response;
        public Exception? Error;
        public Func<Task>? OnLookup;
        public async Task<PayoutRemoteSnapshot?> GetAsync(PayoutLookupRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls); LastRequest = request;
            if (OnLookup is not null) await OnLookup();
            if (Error is not null) throw Error;
            return Response;
        }
    }
}
