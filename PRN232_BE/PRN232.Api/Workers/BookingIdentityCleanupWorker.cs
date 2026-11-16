using PRN232.Application.Services;
using PRN232.Application.Settings;
namespace PRN232.Api.Workers;
public sealed class BookingIdentityCleanupWorker(IServiceScopeFactory scopes,BookingIdentityCleanupSettings settings,ILogger<BookingIdentityCleanupWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){
  if(!settings.Enabled)return;
  using var timer=new PeriodicTimer(TimeSpan.FromHours(1));
  do {try {using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<BookingIdentityCleanupService>().RunAsync(false,stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}catch {logger.LogWarning("Identity retention batch failed; retry on next cycle.");}}
  while(await timer.WaitForNextTickAsync(stoppingToken));
 }
}