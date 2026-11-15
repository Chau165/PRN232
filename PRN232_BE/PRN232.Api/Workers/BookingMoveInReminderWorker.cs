using PRN232.Application.Interfaces;

namespace PRN232.Api.Workers;

// Rebuild 3.2c: mỗi giờ nhắc ngày dọn vào và ngày cuối giữ phòng. Gửi trùng được chặn trong service,
// nên chạy lại nhiều lần (hoặc sau mỗi lần deploy) không tạo thông báo lặp.
public class BookingMoveInReminderWorker(IServiceScopeFactory scopes, ILogger<BookingMoveInReminderWorker> logger)
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
                    var created = await scope.ServiceProvider.GetRequiredService<IBookingMoveInReminderService>()
                        .SendDueRemindersAsync(stoppingToken);
                    if (created > 0)
                    {
                        logger.LogInformation("Sent {Count} booking move-in reminders.", created);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Booking move-in reminder run failed; the next hourly run will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
