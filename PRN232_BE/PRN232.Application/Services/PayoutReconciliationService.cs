using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public sealed class PayoutReconciliationService(IPayoutReconciliationRepository repository, IPayOSPayoutReconciliationGateway gateway,
    PayOSPayoutSettings settings, TimeProvider clock) : IPayoutReconciliationService
{
    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.ReconciliationEnabled) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var leaseSeconds = Math.Max(settings.ReconciliationIntervalSeconds, settings.TimeoutMilliseconds / 1000 + 15);
        var window = new PayoutReconciliationWindow(now, now.AddSeconds(-leaseSeconds),
            now.AddSeconds(-settings.ReconciliationMinimumAgeSeconds), now.AddSeconds(-settings.PayoutRetryDelaySeconds),
            settings.RetryFailedPayouts, settings.MaxPayoutAttempts);
        foreach (var id in await repository.ListDueAsync(window, settings.ReconciliationBatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            now = clock.GetUtcNow().UtcDateTime;
            window = window with { NowUtc = now, DueBeforeUtc = now.AddSeconds(-leaseSeconds),
                StartedBeforeUtc = now.AddSeconds(-settings.ReconciliationMinimumAgeSeconds), RetryBeforeUtc = now.AddSeconds(-settings.PayoutRetryDelaySeconds) };
            var lease = await repository.TryLeaseAsync(id, window, cancellationToken);
            if (lease is null) continue;
            PayoutReconciliationResult result;
            if (lease.Status is PayoutAttemptStatuses.CreateFailed or PayoutAttemptStatuses.TransferFailed)
            {
                if (!settings.RetryFailedPayouts || lease.AttemptNumber >= settings.MaxPayoutAttempts ||
                    !PayoutReconciliationPolicy.HasDefinitiveFailure(lease)) continue;
                result = new(PayoutReconciliationOutcome.Retry);
                if (lease.Status == PayoutAttemptStatuses.TransferFailed)
                {
                    // Check the provider again just before authorizing another transfer.
                    try
                    {
                        var remote = await gateway.GetAsync(new(lease.ReferenceId, lease.ProviderPayoutId), cancellationToken);
                        if (PayoutReconciliationPolicy.Evaluate(lease, remote, clock.GetUtcNow().UtcDateTime,
                            settings.ReconciliationStuckAfterSeconds).Outcome != PayoutReconciliationOutcome.Failed)
                            result = new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_FAILURE_RECHECK_REQUIRED");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception) { result = new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_FAILURE_RECHECK_REQUIRED"); }
                }
            }
            else
            {
                try
                {
                    var remote = await gateway.GetAsync(new(lease.ReferenceId, lease.ProviderPayoutId), cancellationToken);
                    result = PayoutReconciliationPolicy.Evaluate(lease, remote, clock.GetUtcNow().UtcDateTime, settings.ReconciliationStuckAfterSeconds);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { result = new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_LOOKUP_UNAVAILABLE"); }
            }
            using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await repository.ApplyAsync(lease, result, window, clock.GetUtcNow().UtcDateTime, persistence.Token);
        }
    }
}
