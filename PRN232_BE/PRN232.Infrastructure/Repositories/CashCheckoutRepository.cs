using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class CashCheckoutRepository(AppDbContext context) : ICashCheckoutRepository
{
    public Task<PaymentRequests?> GetActiveAsync(int invoiceId, CancellationToken cancellationToken) =>
        context.PaymentRequests.AsNoTracking().SingleOrDefaultAsync(item => item.InvoiceId == invoiceId &&
            (item.Status == PaymentRequestStatuses.Creating || item.Status == PaymentRequestStatuses.Pending ||
             item.Status == PaymentRequestStatuses.RequiresReview), cancellationToken);
}
