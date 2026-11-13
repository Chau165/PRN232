using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IPaymentReconciliationRepository
{
    Task<PaymentReconciliationReadModel?> GetAsync(int id, CancellationToken cancellationToken);
    Task<List<int>> FindDueAsync(DateTime cutoffUtc, int limit, CancellationToken cancellationToken);
    Task<PaymentWebhookReceipts?> GetVerifiedReceiptAsync(int id, CancellationToken cancellationToken);
    Task<string> ApplyAsync(int id, int? ownerId, PayOSRemoteSnapshot? remote, string? errorCode,
        DateTime nowUtc, CancellationToken cancellationToken, bool recheckBooking = false, bool recheckInvoice = false);
}
