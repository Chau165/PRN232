using Microsoft.Extensions.Options;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Api.Workers;

public sealed class PayoutReconciliationWorker(IServiceScopeFactory scopes, IOptions<PayOSPayoutSettings> options,
    ILogger<PayoutReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.ReconciliationEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.ReconciliationIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPayoutReconciliationService>().RunBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("Payout reconciliation batch interrupted; existing claims remain protected."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
