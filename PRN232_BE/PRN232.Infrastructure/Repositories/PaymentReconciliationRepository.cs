using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PaymentReconciliationRepository(AppDbContext context) : IPaymentReconciliationRepository
{
    public Task<PaymentReconciliationReadModel?> GetAsync(int id, CancellationToken cancellationToken) =>
        context.PaymentRequests.AsNoTracking().Where(item => item.PaymentRequestId == id && item.Provider == "PayOS" && item.InvoiceId.HasValue)
            .Select(item => new PaymentReconciliationReadModel(item.PaymentRequestId, item.OrderCode,
                item.Invoice!.Contract!.Room!.Property!.OwnerId, item.Status, item.LastErrorCode, item.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<List<int>> FindDueAsync(DateTime cutoffUtc, int limit, CancellationToken cancellationToken) =>
        context.PaymentRequests.AsNoTracking().Where(item => item.Provider == "PayOS" && item.InvoiceId.HasValue && item.UpdatedAtUtc <= cutoffUtc &&
            (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending ||
             (item.Status == PaymentRequestStatuses.RequiresReview && item.LastErrorCode == "EXPIRED_CHECKOUT_UNRESOLVED")))
            .OrderBy(item => item.UpdatedAtUtc).ThenBy(item => item.PaymentRequestId)
            .Select(item => item.PaymentRequestId).Take(limit).ToListAsync(cancellationToken);

    public Task<PaymentWebhookReceipts?> GetVerifiedReceiptAsync(int id, CancellationToken cancellationToken) =>
        context.PaymentWebhookReceipts.AsNoTracking().Where(item => item.PaymentRequestId == id && item.Outcome == "Verified" &&
            item.DataCode == "00").OrderBy(item => item.PaymentWebhookReceiptId).FirstOrDefaultAsync(cancellationToken);

    public async Task<string> ApplyAsync(int id, int? ownerId, PayOSRemoteSnapshot? remote, string? errorCode,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var invoiceId = await context.PaymentRequests.Where(item => item.PaymentRequestId == id)
            .Select(item => item.InvoiceId).SingleAsync(cancellationToken);
        if (!invoiceId.HasValue) throw new InvoiceStatusLinkConflictException("This request is not an invoice checkout.");
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId} FOR UPDATE", cancellationToken);
        var request = await context.PaymentRequests.SingleAsync(item => item.PaymentRequestId == id, cancellationToken);
        await context.Entry(request).ReloadAsync(cancellationToken);
        var invoice = await context.Invoices.AsNoTracking().Where(item => item.InvoiceId == invoiceId)
            .Select(item => new { item.Status, item.Contract!.CustomerId, item.Contract.Room!.Property!.OwnerId,
                Amounts = item.InvoiceDetails.Select(line => line.Amount).ToList() }).SingleAsync(cancellationToken);
        if (ownerId.HasValue && invoice.OwnerId != ownerId.Value) throw new PaymentRequestStatusForbiddenException();
        if (request.Status == PaymentRequestStatuses.Succeeded) return "AlreadyCompleted";
        if (!Recoverable(request.Status, request.LastErrorCode)) return "ManualReviewOrClosed";

        request.UpdatedAtUtc = nowUtc;
        string result;
        if (remote is null)
        {
            if (request.Status != PaymentRequestStatuses.RequiresReview)
                request.LastErrorCode = errorCode ?? "RECONCILIATION_NOT_FOUND";
            request.LastErrorMessage = "Provider lookup did not confirm the payment request; retry reconciliation.";
            result = errorCode is null ? "NotFoundAtProvider" : "ProviderUnavailable";
            // Absence after the deadline is ambiguous: never release the invoice or create another order blindly.
            if (errorCode is null && request.ExpiresAtUtc <= nowUtc)
            {
                request.Status = PaymentRequestStatuses.RequiresReview;
                request.LastErrorCode = "RECONCILIATION_NOT_FOUND";
                result = "RequiresReview";
            }
        }
        else
        {
            var consistent = remote.OrderCode == request.OrderCode && remote.Amount == request.Amount &&
                request.Provider == "PayOS" && request.Currency == "VND" &&
                !string.IsNullOrWhiteSpace(remote.LinkId) && remote.LinkId.Length <= 128 &&
                remote.LinkId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') &&
                (request.PaymentLinkId is null || request.PaymentLinkId == remote.LinkId) &&
                remote.AmountPaid >= 0 && remote.AmountRemaining >= 0 && remote.AmountPaid <= remote.Amount &&
                remote.AmountPaid + remote.AmountRemaining == remote.Amount &&
                request.CustomerId == invoice.CustomerId && invoice.Amounts.Sum() == request.Amount &&
                (invoice.Status is InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue) && request.PaymentId is null;
            if (!consistent) result = Review(request, "RECONCILIATION_MISMATCH");
            else
            {
                request.PaymentLinkId = remote.LinkId;
                request.LastErrorCode = null;
                request.LastErrorMessage = null;
                if (remote.Status == "PENDING" && remote.AmountPaid == 0 && remote.Transactions.Count == 0)
                {
                    request.Status = PaymentRequestStatuses.Pending;
                    request.CheckoutUrl ??= "https://pay.payos.vn/web/" + Uri.EscapeDataString(remote.LinkId);
                    result = request.ExpiresAtUtc > nowUtc ? "RecoveredPending" : "AwaitingProviderClosure";
                }
                else if ((remote.Status is "CANCELLED" or "EXPIRED") && remote.AmountPaid == 0 && remote.Transactions.Count == 0)
                {
                    request.Status = remote.Status == "CANCELLED" ? PaymentRequestStatuses.Cancelled : PaymentRequestStatuses.Expired;
                    result = "ClosedUnpaid";
                }
                else if (remote.Status == "PROCESSING" && remote.AmountPaid == 0 && remote.Transactions.Count == 0)
                {
                    request.Status = PaymentRequestStatuses.Pending;
                    result = "AwaitingProviderConfirmation";
                }
                else if (remote.Status == "PAID" && remote.AmountRemaining == 0 && remote.AmountPaid == request.Amount &&
                    remote.Transactions.Count == 1 && remote.Transactions[0].Amount == request.Amount &&
                    !string.IsNullOrWhiteSpace(remote.Transactions[0].Reference) && remote.Transactions[0].Reference.Length <= 256 &&
                    remote.Transactions[0].TransactionDateTime?.Length <= 64)
                {
                    request.Status = PaymentRequestStatuses.Pending;
                    result = "ReadyToSettle";
                }
                else result = Review(request, "RECONCILIATION_TRANSFER_REVIEW");
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.Entry(request).State = EntityState.Detached;
        return result;
    }

    public static bool Recoverable(string status, string? errorCode) =>
        status is PaymentRequestStatuses.Creating or PaymentRequestStatuses.Pending ||
        (status == PaymentRequestStatuses.RequiresReview && errorCode == "EXPIRED_CHECKOUT_UNRESOLVED");

    private static string Review(PaymentRequests request, string code)
    {
        request.Status = PaymentRequestStatuses.RequiresReview;
        request.LastErrorCode = code;
        request.LastErrorMessage = "Provider information requires manual review; no payment has been recorded.";
        return "RequiresReview";
    }
}
