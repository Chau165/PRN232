using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class InvoicePaymentLinkRepository(AppDbContext context) : IInvoicePaymentLinkRepository
{
    private async Task LockInvoice(int invoiceId, CancellationToken token)
    {
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Invoices\" WHERE \"InvoiceId\" = {invoiceId} FOR UPDATE", token);
    }

    public async Task<(PaymentRequests Request, bool Created)> ReserveAsync(int invoiceId, int customerId,
        DateTime nowUtc, DateTime expiresUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockInvoice(invoiceId, cancellationToken);
        var invoice = await context.Invoices.AsNoTracking().Where(item => item.InvoiceId == invoiceId)
            .Select(item => new { item.Status, item.Contract!.CustomerId,
                Amounts = item.InvoiceDetails.Select(line => line.Amount).ToList() }).FirstOrDefaultAsync(cancellationToken);
        if (invoice is null) throw new InvoiceNotFoundException("Invoice does not exist.");
        if (invoice.CustomerId != customerId) throw new InvoiceAuthorizationException("You cannot pay another customer's invoice.");
        if (invoice.Status is not (InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue))
            throw new InvoiceStatusLinkConflictException("Invoice is already paid or not payable.");
        var amount = invoice.Amounts.Sum();
        if (amount <= 0 || amount != decimal.Truncate(amount) || amount > 9007199254740991m)
            throw new InvoiceBusinessRuleException("payOS requires a positive whole VND amount within the supported integer range.");
        var active = await context.PaymentRequests.AsNoTracking().SingleOrDefaultAsync(request =>
            request.InvoiceId == invoiceId && (request.Status == PaymentRequestStatuses.Creating ||
                request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.RequiresReview), cancellationToken);
        if (active is not null)
        {
            if (active.Amount != amount || active.CustomerId != customerId || active.Provider != "PayOS")
                throw new InvoiceStatusLinkConflictException("An existing checkout must be reconciled before creating another.");
            await transaction.CommitAsync(cancellationToken);
            return (active, false);
        }
        var created = new PaymentRequests
        {
            InvoiceId = invoiceId, CustomerId = customerId, Amount = amount,
            OrderCode = (long)RandomNumberGenerator.GetInt32(100000000, 999999999) * 1000000 + RandomNumberGenerator.GetInt32(1000000),
            CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc, ExpiresAtUtc = expiresUtc
        };
        context.PaymentRequests.Add(created);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new InvoiceStatusLinkConflictException("A checkout was created concurrently. Retry the same invoice.");
        }
        context.Entry(created).State = EntityState.Detached;
        return (created, true);
    }

    public async Task<PaymentRequests> UpdateAsync(int requestId, string expectedStatus, string status,
        string? linkId, string? checkoutUrl, string? errorCode, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var invoiceId = await context.PaymentRequests.Where(request => request.PaymentRequestId == requestId)
            .Select(request => request.InvoiceId).SingleAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockInvoice(invoiceId, cancellationToken);
        var request = await context.PaymentRequests.SingleAsync(item => item.PaymentRequestId == requestId, cancellationToken);
        await context.Entry(request).ReloadAsync(cancellationToken);
        if (request.Status == expectedStatus)
        {
            var invoice = await context.Invoices.AsNoTracking().Where(item => item.InvoiceId == invoiceId)
                .Select(item => new { item.Status, item.Contract!.CustomerId, Amounts = item.InvoiceDetails.Select(line => line.Amount).ToList() })
                .SingleAsync(cancellationToken);
            request.Status = invoice.Status == InvoiceStatuses.Paid || invoice.CustomerId != request.CustomerId || invoice.Amounts.Sum() != request.Amount
                ? PaymentRequestStatuses.RequiresReview : status;
            request.PaymentLinkId = linkId ?? request.PaymentLinkId;
            request.CheckoutUrl = checkoutUrl ?? request.CheckoutUrl;
            request.LastErrorCode = errorCode;
            request.UpdatedAtUtc = nowUtc;
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        context.Entry(request).State = EntityState.Detached;
        return request;
    }
}
