using PRN232.Domain.Entities;
namespace PRN232.Application.Interfaces;

public interface IInvoicePaymentLinkRepository
{
    Task<(PaymentRequests Request, bool Created)> ReserveAsync(int invoiceId, int customerId,
        DateTime nowUtc, DateTime expiresUtc, CancellationToken cancellationToken);
    Task<PaymentRequests> UpdateAsync(int requestId, string expectedStatus, string status,
        string? linkId, string? checkoutUrl, string? errorCode, DateTime nowUtc, CancellationToken cancellationToken);
}
