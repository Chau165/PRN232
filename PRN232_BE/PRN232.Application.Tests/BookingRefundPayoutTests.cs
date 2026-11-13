using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class BookingRefundPayoutTests
{
    private static readonly PayOSPayoutSettings Enabled = new() { Enabled = true, DispatchEnabled = true };

    internal static async Task<int> ApproveAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<PRN232.Api.Controllers.BookingsController> factory, HttpClient client)
    {
        var bookingId = await BookingRefundRequestApiTests.SeedAsync(factory);
        await LoginAsync(client, "customer@example.test");
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/refund-requests",
            new CreateBookingRefundDto("Đổi kế hoạch", "970422", "001234567890", "TEST CUSTOMER"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<BookingRefundDto>())!.BookingRefundRequestId;
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/booking-refund-requests/{id}/review",
            new ReviewBookingRefundDto(true, 2500000, "Đã đối chiếu tài khoản và số tiền"))).StatusCode);
        return id;
    }

    internal static async Task RunAsync(IServiceProvider services, Gateway gateway, PayOSPayoutSettings? settings = null, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var service = new BookingRefundPayoutService(scope.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>(),
            gateway, gateway, settings ?? Enabled, TimeProvider.System);
        await service.RunBatchAsync(cancellationToken);
    }

    [Fact]
    public async Task Persists_claim_before_sending_and_does_not_repeat_accepted_payout()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        var id = await ApproveAsync(factory, client); var gateway = new Gateway();
        gateway.OnSubmit = async request =>
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var attempt = await db.PayoutAttempts.SingleAsync();
            Assert.Equal("Creating", attempt.Status); Assert.Equal("Refunding", (await db.BookingRefundRequests.SingleAsync()).Status);
            Assert.Equal(id, attempt.BookingRefundRequestId); Assert.Equal(9, attempt.InitiatedByAdminId);
            Assert.Equal(attempt.IdempotencyKey, request.IdempotencyKey); Assert.Equal(attempt.ReferenceId, request.ReferenceId);
            Assert.Equal(2500000, request.Amount); Assert.Equal("001234567890", request.RecipientAccountNumber);
        };
        await RunAsync(factory.Services, gateway); await RunAsync(factory.Services, gateway);
        Assert.Equal(1, gateway.Submits);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.PayoutAttempts.SingleAsync();
        Assert.Equal("Processing", stored.Status); Assert.Equal("provider-payout", stored.ProviderPayoutId);
        Assert.Equal("provider-transaction", stored.ProviderTransactionId); Assert.NotNull(stored.SubmittedAtUtc);
        Assert.Null(stored.CompletedAtUtc); Assert.Null(stored.TransferredAtUtc); Assert.Null(stored.EstimatedFee);
        Assert.Equal("Refunding", (await db.BookingRefundRequests.SingleAsync()).Status);
        Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status); Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal("Succeeded", (await db.PaymentRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("rejected")]
    [InlineData("invalid-response")]
    public async Task Any_submission_error_is_ambiguous_and_never_retried(string failure)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var gateway = new Gateway { SubmitError = failure == "timeout" ? new TimeoutException("private account") :
            new PayOSPayoutGatewayException(failure) };
        await RunAsync(factory.Services, gateway); await RunAsync(factory.Services, gateway);
        Assert.Equal(1, gateway.Submits);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.PayoutAttempts.SingleAsync();
        Assert.Equal("RequiresReview", attempt.Status); Assert.NotNull(attempt.SubmittedAtUtc); Assert.Null(attempt.CompletedAtUtc);
        Assert.Equal("PAYOUT_SUBMISSION_UNCONFIRMED", attempt.LastErrorCode); Assert.Null(attempt.LastErrorMessage);
        Assert.Equal("RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
        Assert.Equal("Deposited", (await db.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preflight_failure_never_sends_and_keeps_credit_units_out_of_vnd_fees(bool timeout)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var gateway = new Gateway { Balance = 1, PreflightError = timeout ? new TimeoutException("private") : null };
        await RunAsync(factory.Services, gateway); await RunAsync(factory.Services, gateway);
        Assert.Equal(0, gateway.Submits); Assert.Equal(1, gateway.Preflights);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.PayoutAttempts.SingleAsync(); Assert.Equal("Create-failed", attempt.Status);
        Assert.NotNull(attempt.CompletedAtUtc); Assert.Null(attempt.SubmittedAtUtc); Assert.Null(attempt.EstimatedFee);
        Assert.Equal("RequiresReview", (await db.BookingRefundRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("payment")]
    [InlineData("admin")]
    public async Task Changed_facts_are_rechecked_before_claim_and_block_dispatch(string change)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (change == "booking") (await db.Bookings.SingleAsync()).Status = "Cancelled";
            if (change == "payment") (await db.PaymentRequests.SingleAsync()).Status = "RequiresReview";
            if (change == "admin") (await db.Users.SingleAsync(item => item.UserId == 9)).IsActive = false;
            await db.SaveChangesAsync();
        }
        var gateway = new Gateway(); await RunAsync(factory.Services, gateway);
        Assert.Equal(0, gateway.Preflights); Assert.Equal(0, gateway.Submits);
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await context.PayoutAttempts.ToListAsync());
        Assert.Equal("RequiresReview", (await context.BookingRefundRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Crash_after_claim_leaves_stable_identifiers_and_is_not_redispatched()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); var id = await ApproveAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>();
            var attempt = await repository.TryClaimAsync(id, DateTime.UtcNow, default);
            Assert.NotNull(attempt); Assert.True(Guid.TryParseExact(attempt.IdempotencyKey, "N", out _));
        }
        var gateway = new Gateway(); await RunAsync(factory.Services, gateway);
        Assert.Equal(0, gateway.Submits);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Creating", (await db.PayoutAttempts.SingleAsync()).Status);
        Assert.Equal("Refunding", (await db.BookingRefundRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Another_worker_cannot_send_a_claimed_request_while_first_http_call_is_in_flight()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new Gateway { OnSubmit = async _ => { entered.SetResult(); await resume.Task; } };
        var first = RunAsync(factory.Services, gateway);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { await RunAsync(factory.Services, gateway); }
        finally { resume.TrySetResult(); }
        await first; Assert.Equal(1, gateway.Submits);
    }

    [Fact]
    public async Task Shutdown_during_submission_records_uncertainty_without_automatic_retry()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        using var cancellation = new CancellationTokenSource();
        var gateway = new Gateway { OnSubmit = _ => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(factory.Services, gateway, cancellationToken: cancellation.Token));
        await RunAsync(factory.Services, gateway); Assert.Equal(1, gateway.Submits);
        using var scope = factory.Services.CreateScope();
        Assert.Equal("RequiresReview", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Disabled_dispatch_does_not_claim_approved_requests()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var gateway = new Gateway(); await RunAsync(factory.Services, gateway, new PayOSPayoutSettings { Enabled = true });
        Assert.Equal(0, gateway.Submits);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.PayoutAttempts.ToListAsync()); Assert.Equal("Approved", (await db.BookingRefundRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Only_explicit_recovery_to_approved_after_definitive_failure_can_create_a_new_attempt()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var gateway = new Gateway { Balance = 1 }; await RunAsync(factory.Services, gateway);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Simulate P9's future verified recovery, not an action P8 performs automatically.
            (await db.BookingRefundRequests.SingleAsync()).Status = "Approved";
            await db.SaveChangesAsync();
        }
        gateway.Balance = 10000000; await RunAsync(factory.Services, gateway);
        using var check = factory.Services.CreateScope();
        var attempts = await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.OrderBy(item => item.AttemptNumber).ToListAsync();
        Assert.Equal(2, attempts.Count); Assert.Equal("Create-failed", attempts[0].Status); Assert.Equal("Processing", attempts[1].Status);
        Assert.Equal(2, attempts[1].AttemptNumber); Assert.NotEqual(attempts[0].ReferenceId, attempts[1].ReferenceId);
        Assert.NotEqual(attempts[0].IdempotencyKey, attempts[1].IdempotencyKey); Assert.Equal(1, gateway.Submits);
    }

    [Fact]
    public async Task Database_failure_after_provider_acceptance_keeps_claim_and_cannot_resend()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient(); await ApproveAsync(factory, client);
        var gateway = new Gateway();
        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRefundPayoutRepository>();
            var service = new BookingRefundPayoutService(new FailingOutcomeRepository(repository), gateway, gateway, Enabled, TimeProvider.System);
            await Assert.ThrowsAsync<IOException>(() => service.RunBatchAsync(default));
        }
        await RunAsync(factory.Services, gateway); Assert.Equal(1, gateway.Submits);
        using var check = factory.Services.CreateScope();
        Assert.Equal("Creating", (await check.ServiceProvider.GetRequiredService<AppDbContext>().PayoutAttempts.SingleAsync()).Status);
    }

    private sealed class FailingOutcomeRepository(IBookingRefundPayoutRepository inner) : IBookingRefundPayoutRepository
    {
        public Task<IReadOnlyList<int>> ListApprovedAsync(int take, CancellationToken cancellationToken) => inner.ListApprovedAsync(take, cancellationToken);
        public Task<PRN232.Domain.Entities.PayoutAttempts?> TryClaimAsync(int id, DateTime now, CancellationToken cancellationToken) => inner.TryClaimAsync(id, now, cancellationToken);
        public Task RecordOutcomeAsync(int id, PayoutDispatchOutcome outcome, PayoutSubmissionReceipt? receipt, string? error,
            DateTime now, DateTime? submittedAt, CancellationToken cancellationToken) => throw new IOException("DB unavailable after HTTP");
    }

    internal sealed class Gateway : IPayOSPayoutGateway, IPayOSPayoutSubmissionGateway
    {
        public int Submits, Preflights;
        public decimal Balance = 10000000;
        public Exception? PreflightError, SubmitError;
        public Func<PayoutSubmissionRequest, Task>? OnSubmit;
        public Task<PayoutBalanceSnapshot> GetBalanceAsync(CancellationToken cancellationToken) => Task.FromResult(new PayoutBalanceSnapshot(Balance, "VND", DateTime.UtcNow));
        public Task<PayoutCostEstimate> EstimateAsync(PayoutPreflightRequest request, CancellationToken cancellationToken) => Task.FromResult(new PayoutCostEstimate(999, true, DateTime.UtcNow));
        public async Task<PayoutPreflightSnapshot> PreflightAsync(PayoutPreflightRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Preflights);
            if (PreflightError is not null) throw PreflightError;
            return new(await GetBalanceAsync(cancellationToken), await EstimateAsync(request, cancellationToken), Balance >= request.Amount);
        }
        public async Task<PayoutSubmissionReceipt> SubmitAsync(PayoutSubmissionRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Submits);
            if (OnSubmit is not null) await OnSubmit(request);
            if (SubmitError is not null) throw SubmitError;
            return new("provider-payout", "provider-transaction", "Processing", "Processing");
        }
    }
}
