using Microsoft.Extensions.Options;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;

namespace PRN232.Api.Workers;

public class PaymentReconciliationWorker(IServiceScopeFactory scopes, IOptions<PayOSSettings> options,
    ILogger<PaymentReconciliationWorker> logger) : BackgroundService
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
                    await scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>().RunBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("payOS reconciliation batch failed; the next scheduled batch will retry."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
