using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class CashCheckoutCoordinator(ICashCheckoutRepository repository, IPayOSReconciliationGateway lookup,
    IPayOSCancellationGateway cancellation, IPaymentReconciliationRepository reconciliation,
    IPayOSWebhookRepository settlement, PayOSSettings settings, TimeProvider clock) : ICashCheckoutCoordinator
{
    public async Task PrepareAsync(int invoiceId, int? ownerId, CancellationToken cancellationToken)
    {
        var request = await repository.GetActiveAsync(invoiceId, cancellationToken);
        if (request is null) return;
        if (!settings.Enabled || request.Provider != "PayOS" ||
            (request.Status == PaymentRequestStatuses.RequiresReview && request.LastErrorCode != "EXPIRED_CHECKOUT_UNRESOLVED"))
            throw new InvoiceConflictException("The online checkout requires reconciliation before collecting cash.");

        PayOSRemoteSnapshot? remote = null;
        string? error = null;
        try
        {
            remote = await lookup.GetAsync(request.OrderCode, cancellationToken);
            // Validate identity before issuing a provider mutation. The final DB operation validates again under its invoice lock.
            var matches = remote is not null && remote.OrderCode == request.OrderCode && remote.Amount == request.Amount &&
                request.Currency == "VND" && remote.AmountRemaining == request.Amount &&
                !string.IsNullOrWhiteSpace(remote.LinkId) && remote.LinkId.Length <= 128 &&
                remote.LinkId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') &&
                (request.PaymentLinkId is null || request.PaymentLinkId == remote.LinkId);
            if (matches && remote!.Status == "PENDING" && remote.AmountPaid == 0 && remote.Transactions.Count == 0)
            {
                try { await cancellation.CancelAsync(request.OrderCode, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { /* A lost cancel response is ambiguous; always re-read before deciding. */ }
                remote = await lookup.GetAsync(request.OrderCode, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { remote = null; error = "CASH_CHECKOUT_LOOKUP_FAILED"; }

        string result;
        try
        {
            result = await reconciliation.ApplyAsync(request.PaymentRequestId, ownerId, remote, error,
                clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
        catch (PaymentRequestStatusForbiddenException)
        {
            throw new InvoiceAuthorizationException("You no longer manage this property's invoice.");
        }
        if (result == "ClosedUnpaid") return;
        if (result == "ReadyToSettle")
        {
            var transfer = remote!.Transactions.Single();
            var receipt = new PaymentWebhookReceipts
            {
                Source = "Reconciliation", OrderCode = remote.OrderCode, PaymentLinkId = remote.LinkId,
                Amount = transfer.Amount, Currency = "VND", DataCode = "00", Reference = transfer.Reference,
                TransactionDateTime = transfer.TransactionDateTime, ReceivedAtUtc = clock.GetUtcNow().UtcDateTime
            };
            receipt.Fingerprint = PayOSReceiptFactory.Fingerprint(receipt);
            await settlement.RecordReconciliationAsync(receipt, cancellationToken);
        }
        throw new InvoiceConflictException("Cash was not recorded. The online checkout is paid, unresolved, or changed; refresh and reconcile before collecting cash.");
    }
}
