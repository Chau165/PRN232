using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingPaymentLinkRepository
{
    Task<(PaymentRequests Request, bool Created)> ReserveAsync(int bookingId, int customerId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<PaymentRequests> UpdateAsync(int requestId, string status, string? linkId, string? checkoutUrl, string? error,
        DateTime nowUtc, CancellationToken cancellationToken);
}
