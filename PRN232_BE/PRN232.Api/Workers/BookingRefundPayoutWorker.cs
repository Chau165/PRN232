using Microsoft.Extensions.Options;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Api.Workers;

public sealed class BookingRefundPayoutWorker(IServiceScopeFactory scopes, IOptions<PayOSPayoutSettings> options,
    ILogger<BookingRefundPayoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.DispatchEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.DispatchIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IBookingRefundPayoutService>().RunBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("Booking refund payout batch interrupted. Claimed attempts require reconciliation before any retry."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
