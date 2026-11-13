using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class PaymentReconciliationService(IPaymentReconciliationRepository repository,
    IPayOSReconciliationGateway gateway, IPayOSWebhookRepository settlement, PayOSSettings settings,
    TimeProvider timeProvider, IPropertyRepository properties) : IPaymentReconciliationService
{
    public async Task<PaymentReconciliationDto?> ReconcileAsync(int id, int userId, string role, CancellationToken cancellationToken, bool recheckBooking = false, bool recheckInvoice = false)
    {
        if (id <= 0) throw new PaymentRequestStatusValidationException();
        if (userId <= 0 || role is not ("Admin" or "Manager")) throw new PaymentRequestStatusForbiddenException();
        if ((recheckBooking || recheckInvoice) && role != "Admin") throw new PaymentRequestStatusForbiddenException();
        var request = await repository.GetAsync(id, cancellationToken);
        if (request is null) return null;
        if (role == "Manager" && !await properties.IsManagerAssignedAsync(request.PropertyId, userId))
            throw new PaymentRequestStatusForbiddenException();
        if (!settings.Enabled) throw new CheckoutUnavailableException();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (request.Status == PaymentRequestStatuses.Succeeded) return new(id, "AlreadyCompleted", request.Status, now);
        if (!(request.Status is PaymentRequestStatuses.Creating or PaymentRequestStatuses.Pending ||
            (request.Status == PaymentRequestStatuses.RequiresReview && (request.LastErrorCode == "EXPIRED_CHECKOUT_UNRESOLVED" || recheckBooking && BookingPaymentTimePolicy.IsReviewCode(request.LastErrorCode) || recheckInvoice && request.LastErrorCode == PayOSPaymentTimePolicy.InvoiceReviewCode))))
            return new(id, "ManualReviewOrClosed", request.Status, now);
        if (request.UpdatedAtUtc > now.AddSeconds(-settings.ReconciliationMinAgeSeconds))
            return new(id, "TooRecent", request.Status, now);

        // Replay a durable B7 event first. Its original receipt time remains the evidence timestamp.
        var verified = await repository.GetVerifiedReceiptAsync(id, cancellationToken);
        if (verified is not null && request.Status != PaymentRequestStatuses.RequiresReview)
        {
            verified.ReceivedAtUtc = now; // incoming replay confirmation time; stored evidence time is unchanged
            if (verified.Source == "Webhook") await settlement.RecordAsync(verified, true, cancellationToken);
            else await settlement.RecordReconciliationAsync(verified, cancellationToken);
            var replayed = (await repository.GetAsync(id, cancellationToken))!;
            return new(id, "ReplayedReceipt", replayed.Status, now);
        }

        PayOSRemoteSnapshot? remote = null;
        string? error = null;
        try { remote = await gateway.GetAsync(request.OrderCode, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { error = "RECONCILIATION_LOOKUP_FAILED"; } // never persist provider secrets/raw errors
        now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.ApplyAsync(id, role == "Manager" ? userId : null, remote, error, now, cancellationToken, recheckBooking, recheckInvoice);
        if (result == "ReadyToSettle")
        {
            var transfer = remote!.Transactions.Single();
            // The authenticated V2 lookup does not expose currency. This channel and the stored order are VND-only.
            var receipt = new PaymentWebhookReceipts
            {
                Source = "Reconciliation", OrderCode = remote.OrderCode, PaymentLinkId = remote.LinkId,
                Amount = transfer.Amount, Currency = "VND", DataCode = "00", Reference = transfer.Reference,
                TransactionDateTime = transfer.TransactionDateTime, ReceivedAtUtc = now
            };
            receipt.Fingerprint = PayOSReceiptFactory.Fingerprint(receipt);
            await settlement.RecordReconciliationAsync(receipt, cancellationToken, recheckBooking, recheckInvoice);
            result = "ReconciledPayment";
        }
        var updated = (await repository.GetAsync(id, cancellationToken))!;
        if (updated.Status == PaymentRequestStatuses.RequiresReview) result = "RequiresReview";
        return new(id, result, updated.Status, now);
    }

    public async Task<int> RunBatchAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.ReconciliationEnabled) return 0;
        var ids = await repository.FindDueAsync(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(-settings.ReconciliationMinAgeSeconds),
            settings.ReconciliationBatchSize, cancellationToken);
        foreach (var id in ids)
            await ReconcileAsync(id, 1, "Admin", cancellationToken);
        return ids.Count;
    }
}
