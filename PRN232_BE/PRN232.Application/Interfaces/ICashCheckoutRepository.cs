using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface ICashCheckoutRepository
{
    Task<PaymentRequests?> GetActiveAsync(int invoiceId, CancellationToken cancellationToken);
}
