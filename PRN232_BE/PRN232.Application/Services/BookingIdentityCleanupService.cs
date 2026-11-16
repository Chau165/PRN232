using PRN232.Application.Interfaces;
using PRN232.Application.Settings;
namespace PRN232.Application.Services;
public sealed class BookingIdentityCleanupService(IBookingIdentityCleanupRepository repository,BookingIdentityCleanupSettings settings,TimeProvider clock)
{
    public async Task<BookingIdentityCleanupReport> RunAsync(bool previewOnly,CancellationToken token)
    {
        settings.Validate();var now=clock.GetUtcNow().UtcDateTime;DateTime? cutoff=settings.DraftRetentionDays.HasValue?now.AddDays(-settings.DraftRetentionDays.Value):null;
        var counts=await repository.PreviewAsync(cutoff,now,token);
        if(previewOnly||!settings.Enabled||settings.DryRun||cutoff is null)return new(settings.Enabled,settings.DryRun,cutoff.HasValue,counts.Drafts,counts.Tasks);
        var staged=await repository.StageAsync(cutoff.Value,now,settings.BatchSize,token);
        var processed=await repository.ProcessAsync(now,settings.BatchSize,token);
        var purged=await repository.PurgeAsync(settings.BatchSize,now,token);
        return new(settings.Enabled,settings.DryRun,true,counts.Drafts,counts.Tasks,staged,processed,purged);
    }
}
