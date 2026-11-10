using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PayOSWebhookRepository(AppDbContext context) : IPayOSWebhookRepository
{
    public async Task RecordAsync(PaymentWebhookReceipts receipt, bool providerSuccess, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"PayOS:" + receipt.Reference}, 0))", cancellationToken);
        var existing = await context.PaymentWebhookReceipts.SingleOrDefaultAsync(item =>
            item.Provider == "PayOS" && item.Reference == receipt.Reference, cancellationToken);
        if (existing is not null)
        {
            if (existing.Fingerprint != receipt.Fingerprint)
            {
                existing.Outcome = "RequiresReview";
                existing.ConflictingFingerprint = receipt.Fingerprint;
                if (existing.PaymentRequestId.HasValue)
                {
                    var original = await LockRequestAsync(existing.OrderCode, cancellationToken);
                    if (original is not null) MarkReview(original, "WEBHOOK_REFERENCE_CONFLICT", receipt.ReceivedAtUtc);
                }
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (existing.Outcome == "Verified" && providerSuccess)
            {
                // Replay a durable B7 event that predates settlement support.
                var replayRequest = await LockRequestAsync(existing.OrderCode, cancellationToken);
                if (replayRequest is not null) await SettleAsync(existing, replayRequest, receipt.ReceivedAtUtc, cancellationToken);
                else existing.Outcome = "RequiresReview";
                await context.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var request = await LockRequestAsync(receipt.OrderCode, cancellationToken);
        receipt.PaymentRequestId = request?.PaymentRequestId;
        if (request is null) receipt.Outcome = "UnknownOrder"; // includes provider registration probes
        else if (!providerSuccess) receipt.Outcome = "IgnoredStatus";
        else
        {
            await SettleAsync(receipt, request, receipt.ReceivedAtUtc, cancellationToken);
        }
        context.PaymentWebhookReceipts.Add(receipt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SettleAsync(PaymentWebhookReceipts receipt, PaymentRequests request, DateTime confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.AsNoTracking().Where(item => item.InvoiceId == request.InvoiceId)
            .Select(item => new { item.Status, item.InvoiceMonth, item.Contract!.CustomerId,
                item.Contract.Room!.RoomCode, item.Contract.Room.Property!.OwnerId,
                Amounts = item.InvoiceDetails.Select(line => line.Amount).ToList() }).SingleAsync(cancellationToken);
        var validTime = PayOSTransactionTime.TryGetUtc(receipt.TransactionDateTime, out var paidAtUtc) &&
            paidAtUtc >= request.CreatedAtUtc && request.ExpiresAtUtc.HasValue &&
            paidAtUtc < request.ExpiresAtUtc && paidAtUtc <= receipt.ReceivedAtUtc;
        var eligible = request.Provider == "PayOS" && request.Currency == "VND" && receipt.Currency == "VND" &&
            receipt.DataCode == "00" && receipt.Amount > 0 && receipt.Amount == request.Amount &&
            receipt.Amount == invoice.Amounts.Sum() && request.CustomerId == invoice.CustomerId &&
            request.PaymentLinkId == receipt.PaymentLinkId && request.PaymentId is null &&
            (request.Status is PaymentRequestStatuses.Creating or PaymentRequestStatuses.Pending) &&
            (invoice.Status is InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue) &&
            (request.ProviderReference is null || request.ProviderReference == receipt.Reference) && validTime;
        request.WebhookReceivedAtUtc ??= receipt.ReceivedAtUtc;
        request.UpdatedAtUtc = confirmedAtUtc;
        if (!eligible)
        {
            receipt.Outcome = "RequiresReview";
            MarkReview(request, validTime ? "WEBHOOK_REQUIRES_REVIEW" : "WEBHOOK_TRANSACTION_TIME_INVALID", confirmedAtUtc);
            return;
        }

        var changed = await context.Invoices.Where(item => item.InvoiceId == request.InvoiceId &&
            (item.Status == InvoiceStatuses.Unpaid || item.Status == InvoiceStatuses.Overdue))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, InvoiceStatuses.Paid), cancellationToken);
        if (changed != 1)
        {
            receipt.Outcome = "RequiresReview";
            MarkReview(request, "INVOICE_SETTLEMENT_CONFLICT", confirmedAtUtc);
            return;
        }

        // The caller holds the invoice lock and transaction. One SaveChanges/commit
        // covers this payment, its request link, receipt, notifications and invoice.
        request.Payment = new Payments
        {
            InvoiceId = request.InvoiceId, UserId = request.CustomerId, Amount = receipt.Amount,
            PaymentMethod = PaymentMethods.PayOS, Status = PaymentStatuses.Success, PaidAtUtc = paidAtUtc
        };
        request.Status = PaymentRequestStatuses.Succeeded;
        request.ProviderReference = receipt.Reference;
        request.ConfirmedAtUtc = confirmedAtUtc;
        request.LastErrorCode = null;
        request.LastErrorMessage = null;
        receipt.Outcome = "Settled";
        var period = invoice.InvoiceMonth.ToString("MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var userId in new[] { request.CustomerId, invoice.OwnerId }.Distinct())
            context.Notifications.Add(new Notifications
            {
                UserId = userId, Type = NotificationTypes.Invoice, IsRead = false,
                Title = $"Đã thanh toán hóa đơn tháng {period} - Phòng {invoice.RoomCode} qua payOS"
            });
    }

    private async Task<PaymentRequests?> LockRequestAsync(long orderCode, CancellationToken cancellationToken)
    {
        var invoiceId = await context.PaymentRequests.Where(request => request.OrderCode == orderCode)
            .Select(request => (int?)request.InvoiceId).FirstOrDefaultAsync(cancellationToken);
        if (!invoiceId.HasValue) return null;
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId.Value} FOR UPDATE", cancellationToken);
        // Same invoice-first lock order as checkout creation and settlement.
        var result = await context.PaymentRequests.SingleAsync(request => request.OrderCode == orderCode, cancellationToken);
        await context.Entry(result).ReloadAsync(cancellationToken);
        return result;
    }

    private static void MarkReview(PaymentRequests request, string code, DateTime nowUtc)
    {
        // Do not reactivate an old terminal request when a newer one holds the active index.
        if (request.Status is PaymentRequestStatuses.Creating or PaymentRequestStatuses.Pending or PaymentRequestStatuses.RequiresReview)
            request.Status = PaymentRequestStatuses.RequiresReview;
        request.LastErrorCode = code;
        request.UpdatedAtUtc = nowUtc;
    }
}
