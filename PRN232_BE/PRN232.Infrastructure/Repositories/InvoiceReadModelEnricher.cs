using Microsoft.EntityFrameworkCore;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

internal static class InvoiceReadModelEnricher
{
    public static async Task<List<InvoiceReadModel>> WithTotalsAndPaymentsAsync(
        AppDbContext context,
        List<InvoiceReadModel> invoices,
        CancellationToken cancellationToken = default)
    {
        if (invoices.Count == 0)
        {
            return invoices;
        }

        var invoiceIds = invoices.Select(invoice => invoice.InvoiceId).ToArray();
        var payments = await context.Payments
            .AsNoTracking()
            .Where(payment => payment.InvoiceId.HasValue &&
                invoiceIds.Contains(payment.InvoiceId.Value) &&
                payment.Status == PaymentStatuses.Success)
            .OrderBy(payment => payment.PaymentId)
            .Select(payment => new
            {
                InvoiceId = payment.InvoiceId!.Value,
                payment.Amount,
                payment.PaymentMethod
            })
            .ToListAsync(cancellationToken);
        var paymentsByInvoice = payments
            .GroupBy(payment => payment.InvoiceId)
            .ToDictionary(group => group.Key, group => new
            {
                AmountPaid = group.Sum(payment => payment.Amount),
                PaymentMethod = group.Last().PaymentMethod
            });

        return invoices.Select(invoice =>
        {
            paymentsByInvoice.TryGetValue(invoice.InvoiceId, out var paymentSummary);
            return invoice with
            {
                Total = invoice.Lines.Sum(line => line.Amount),
                PrepaidRentApplied = invoice.Lines
                    .Where(line => line.ItemType == InvoiceItemTypes.PrepaidRentCredit && line.Amount < 0m)
                    .Sum(line => -line.Amount),
                AmountPaid = paymentSummary?.AmountPaid ?? 0m,
                PaymentMethod = paymentSummary?.PaymentMethod
            };
        }).ToList();
    }
}
