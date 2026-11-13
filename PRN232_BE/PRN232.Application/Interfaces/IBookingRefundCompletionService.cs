namespace PRN232.Application.Interfaces;

public interface IBookingRefundCompletionService
{
    Task RunBatchAsync(CancellationToken cancellationToken);
}

public interface IBookingRefundCompletionRepository
{
    Task<IReadOnlyList<int>> ListUnfinishedAsync(int take, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(int attemptId, DateTime now, CancellationToken cancellationToken);
}
