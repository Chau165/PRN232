using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PayOSWebhookRepository(AppDbContext context) : IPayOSWebhookRepository
{
    public Task RecordAsync(PaymentWebhookReceipts receipt, bool providerSuccess, CancellationToken cancellationToken) =>
        RecordCoreAsync(receipt, providerSuccess, true, cancellationToken);

    public Task RecordReconciliationAsync(PaymentWebhookReceipts receipt, CancellationToken cancellationToken) =>
        RecordCoreAsync(receipt, true, false, cancellationToken);

    private async Task RecordCoreAsync(PaymentWebhookReceipts receipt, bool providerSuccess, bool isWebhook, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"PayOS:" + receipt.Reference}, 0))", cancellationToken);
        var existing = await context.PaymentWebhookReceipts.SingleOrDefaultAsync(item =>
            item.Provider == "PayOS" && item.Reference == receipt.Reference, cancellationToken);
        if (existing is not null)
        {
            if (existing.Fingerprint != receipt.Fingerprint && !Equivalent(existing, receipt))
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
                if (replayRequest is not null) await SettleAsync(existing, replayRequest, receipt.ReceivedAtUtc, existing.Source == "Webhook", cancellationToken);
                else existing.Outcome = "RequiresReview";
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (isWebhook && providerSuccess && existing.Source == "Reconciliation" && existing.Outcome == "Settled")
            {
                var settledRequest = await LockRequestAsync(existing.OrderCode, cancellationToken);
                if (settledRequest is not null) settledRequest.WebhookReceivedAtUtc ??= receipt.ReceivedAtUtc;
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
            await SettleAsync(receipt, request, receipt.ReceivedAtUtc, isWebhook, cancellationToken);
        }
        context.PaymentWebhookReceipts.Add(receipt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SettleAsync(PaymentWebhookReceipts receipt, PaymentRequests request, DateTime confirmedAtUtc, bool isWebhook,
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
        if (isWebhook) request.WebhookReceivedAtUtc ??= receipt.ReceivedAtUtc;
        request.UpdatedAtUtc = confirmedAtUtc;
        // A new reference is additional money, not a replay of the original payment.
        // Preserve it for Admin review without crediting the invoice/revenue twice.
        var authenticTransfer = request.Provider == "PayOS" && request.Currency == "VND" && receipt.Currency == "VND" &&
            receipt.DataCode == "00" && receipt.Amount > 0 && request.PaymentLinkId == receipt.PaymentLinkId &&
            PayOSTransactionTime.TryGetUtc(receipt.TransactionDateTime, out var transferAtUtc) &&
            transferAtUtc >= request.CreatedAtUtc && transferAtUtc <= receipt.ReceivedAtUtc;
        if (invoice.Status == InvoiceStatuses.Paid && authenticTransfer)
        {
            var originalPaymentId = await context.Payments.AsNoTracking()
                .Where(item => item.InvoiceId == request.InvoiceId && item.Status == PaymentStatuses.Success)
                .OrderBy(item => item.PaymentId).Select(item => (int?)item.PaymentId).FirstOrDefaultAsync(cancellationToken);
            if (originalPaymentId.HasValue)
            {
                receipt.Outcome = "DuplicatePayment";
                MarkReview(request, "POSSIBLE_DUPLICATE_PAYMENT", confirmedAtUtc);
                context.PaymentReviewCases.Add(new PaymentReviewCases
                {
                    Receipt = receipt, InvoiceId = request.InvoiceId, ExistingPaymentId = originalPaymentId.Value,
                    CreatedAtUtc = confirmedAtUtc
                });
                var admins = await context.Users.AsNoTracking().Where(item => item.IsActive && item.Role!.RoleName == RoleNames.Admin)
                    .Select(item => item.UserId).ToListAsync(cancellationToken);
                foreach (var admin in admins)
                    context.Notifications.Add(new Notifications { UserId = admin, Type = NotificationTypes.Invoice,
                        Title = $"Cần kiểm tra khoản trả trùng hóa đơn #{request.InvoiceId}, mã đơn {request.OrderCode}", IsRead = false });
                return;
            }
        }
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

    private static bool Equivalent(PaymentWebhookReceipts first, PaymentWebhookReceipts second) =>
        first.OrderCode == second.OrderCode && first.PaymentLinkId == second.PaymentLinkId &&
        first.Amount == second.Amount && first.Currency == second.Currency && first.DataCode == second.DataCode &&
        first.Reference == second.Reference && PayOSTransactionTime.TryGetUtc(first.TransactionDateTime, out var a) &&
        PayOSTransactionTime.TryGetUtc(second.TransactionDateTime, out var b) && a == b;
}
