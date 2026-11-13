using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;

namespace PRN232.Application.Services;

public sealed class BookingRefundPayoutService(IBookingRefundPayoutRepository repository, IPayOSPayoutGateway preflight,
    IPayOSPayoutSubmissionGateway gateway, PayOSPayoutSettings settings, TimeProvider clock) : IBookingRefundPayoutService
{
    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.DispatchEnabled) return;
        foreach (var id in await repository.ListApprovedAsync(settings.DispatchBatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = await repository.TryClaimAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken);
            if (attempt is null) continue;
            var request = new PayoutSubmissionRequest(attempt.ReferenceId, attempt.IdempotencyKey, attempt.Amount,
                attempt.RecipientBankBin, attempt.RecipientAccountNumber, attempt.Description);
            PayoutSubmissionReceipt? receipt = null;
            DateTime? submittedAt = null;
            var outcome = PayoutDispatchOutcome.NotSent;
            string? error = null;
            try
            {
                var check = await preflight.PreflightAsync(new(request.ReferenceId, request.Amount,
                    request.RecipientBankBin, request.RecipientAccountNumber, request.Description), cancellationToken);
                if (!check.HasSufficientPrincipalBalance || check.Balance.Currency != "VND" || check.Balance.Balance < request.Amount)
                    error = "PAYOUT_INSUFFICIENT_PRINCIPAL";
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // From here onward any error is ambiguous: never automatically issue another transfer.
                    outcome = PayoutDispatchOutcome.Unknown;
                    submittedAt = clock.GetUtcNow().UtcDateTime;
                    receipt = await gateway.SubmitAsync(request, cancellationToken);
                    outcome = PayoutDispatchOutcome.Accepted;
                }
            }
            catch (Exception)
            {
                // Never persist provider exception messages (credentials, bank/account details).
                error = submittedAt is null ? "PAYOUT_PREFLIGHT_UNAVAILABLE" : "PAYOUT_SUBMISSION_UNCONFIRMED";
            }
            // Persist even on graceful shutdown; this independent short timeout cannot re-send money.
            using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await repository.RecordOutcomeAsync(attempt.PayoutAttemptId, outcome, receipt, error,
                clock.GetUtcNow().UtcDateTime, submittedAt, persistence.Token);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
