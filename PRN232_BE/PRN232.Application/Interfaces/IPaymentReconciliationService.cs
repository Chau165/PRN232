using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPaymentReconciliationService
{
    Task<PaymentReconciliationDto?> ReconcileAsync(int id, int userId, string role, CancellationToken cancellationToken, bool recheckBooking = false, bool recheckInvoice = false);
    Task<int> RunBatchAsync(CancellationToken cancellationToken);
}
