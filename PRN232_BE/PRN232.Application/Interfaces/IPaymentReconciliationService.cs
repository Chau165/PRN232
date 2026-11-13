using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPaymentReconciliationService
{
    Task<PaymentReconciliationDto?> ReconcileAsync(int id, int userId, string role, CancellationToken cancellationToken);
    Task<int> RunBatchAsync(CancellationToken cancellationToken);
}
