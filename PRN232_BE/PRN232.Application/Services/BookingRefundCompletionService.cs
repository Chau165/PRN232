using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Application.Services;

// Recovery for successes recorded before P10, or after an interrupted finalization.
public sealed class BookingRefundCompletionService(IBookingRefundCompletionRepository repository,
    PayOSPayoutSettings settings, TimeProvider clock) : IBookingRefundCompletionService
{
    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.ReconciliationEnabled) return;
        foreach (var id in await repository.ListUnfinishedAsync(settings.ReconciliationBatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await repository.CompleteAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
    }
}
