using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
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
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var request = await LockRequestAsync(receipt.OrderCode, cancellationToken);
        receipt.PaymentRequestId = request?.PaymentRequestId;
        if (request is null) receipt.Outcome = "UnknownOrder"; // includes provider registration probes
        else if (!providerSuccess) receipt.Outcome = "IgnoredStatus";
        else
        {
            var invoice = await context.Invoices.AsNoTracking().Where(item => item.InvoiceId == request.InvoiceId)
                .Select(item => new { item.Status, item.Contract!.CustomerId,
                    Amounts = item.InvoiceDetails.Select(line => line.Amount).ToList() }).SingleAsync(cancellationToken);
            var matches = request.Provider == "PayOS" && request.Currency == "VND" && receipt.Currency == "VND" &&
                receipt.Amount > 0 && receipt.Amount == request.Amount && receipt.Amount == invoice.Amounts.Sum() &&
                request.CustomerId == invoice.CustomerId && request.PaymentLinkId == receipt.PaymentLinkId;
            var eligible = (request.Status is PaymentRequestStatuses.Creating or PaymentRequestStatuses.Pending) &&
                (invoice.Status is InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue) &&
                (request.ProviderReference is null || request.ProviderReference == receipt.Reference) &&
                request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc > receipt.ReceivedAtUtc;
            receipt.Outcome = matches && eligible ? "Verified" : "RequiresReview";
            if (receipt.Outcome == "RequiresReview") MarkReview(request, "WEBHOOK_REQUIRES_REVIEW", receipt.ReceivedAtUtc);
            request.WebhookReceivedAtUtc ??= receipt.ReceivedAtUtc;
            if (matches && request.ProviderReference is null) request.ProviderReference = receipt.Reference;
            request.UpdatedAtUtc = receipt.ReceivedAtUtc;
        }
        context.PaymentWebhookReceipts.Add(receipt);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
