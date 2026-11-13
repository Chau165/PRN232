using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingRefundPayoutService
{
    Task RunBatchAsync(CancellationToken cancellationToken);
}

public interface IBookingRefundPayoutRepository
{
    Task<IReadOnlyList<int>> ListApprovedAsync(int take, CancellationToken cancellationToken);
    // Commits the immutable snapshot and ownership of the dispatch before any external request.
    Task<PayoutAttempts?> TryClaimAsync(int refundRequestId, DateTime now, CancellationToken cancellationToken);
    Task RecordOutcomeAsync(int attemptId, PayoutDispatchOutcome outcome, PayoutSubmissionReceipt? receipt,
        string? errorCode, DateTime now, DateTime? submittedAt, CancellationToken cancellationToken);
}

// Kept separate from P5's read-only preflight gateway.
public interface IPayOSPayoutSubmissionGateway
{
    Task<PayoutSubmissionReceipt> SubmitAsync(PayoutSubmissionRequest request, CancellationToken cancellationToken);
}
