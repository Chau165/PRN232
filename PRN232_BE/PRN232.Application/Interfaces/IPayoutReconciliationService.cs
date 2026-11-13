using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IPayoutReconciliationService
{
    Task RunBatchAsync(CancellationToken cancellationToken);
}

public interface IPayOSPayoutReconciliationGateway
{
    Task<PayoutRemoteSnapshot?> GetAsync(PayoutLookupRequest request, CancellationToken cancellationToken);
}

public interface IPayoutReconciliationRepository
{
    Task<IReadOnlyList<int>> ListDueAsync(PayoutReconciliationWindow window, int take, CancellationToken cancellationToken);
    Task<PayoutAttempts?> TryLeaseAsync(int attemptId, PayoutReconciliationWindow window, CancellationToken cancellationToken);
    Task ApplyAsync(PayoutAttempts lease, PayoutReconciliationResult result, PayoutReconciliationWindow window, DateTime now, CancellationToken cancellationToken);
}
