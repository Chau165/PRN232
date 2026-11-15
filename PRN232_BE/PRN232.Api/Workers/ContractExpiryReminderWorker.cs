using PRN232.Application.Interfaces;

namespace PRN232.Api.Workers;

// Rebuild 3.4: mỗi giờ quét hợp đồng sắp hết/đã hết hạn và gửi nhắc. Gửi trùng được chặn trong
// service, nên chạy lại nhiều lần (hoặc sau mỗi lần deploy) không tạo thông báo lặp.
public class ContractExpiryReminderWorker(IServiceScopeFactory scopes, ILogger<ContractExpiryReminderWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var created = await scope.ServiceProvider.GetRequiredService<IContractExpiryReminderService>()
                        .SendDueRemindersAsync(stoppingToken);
                    if (created > 0)
                    {
                        logger.LogInformation("Sent {Count} contract expiry reminders.", created);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Contract expiry reminder run failed; the next hourly run will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
