namespace PRN232.Application.Interfaces;
public interface IBookingIdentityCleanupQueue
{
    Task EnqueueAsync(string objectPath,DateTime now,CancellationToken token);
}
public record BookingIdentityCleanupReport(bool Enabled,bool DryRun,bool PolicyConfigured,int EligibleDrafts,int PendingObjects,int Staged=0,int Processed=0,int Purged=0);
public interface IBookingIdentityCleanupRepository : IBookingIdentityCleanupQueue
{
    Task<(int Drafts,int Tasks)> PreviewAsync(DateTime? cutoff,DateTime now,CancellationToken token);
    Task<int> StageAsync(DateTime cutoff,DateTime now,int batch,CancellationToken token);
    Task<int> ProcessAsync(DateTime now,int batch,CancellationToken token);
    Task<int> PurgeAsync(int batch,DateTime now,CancellationToken token);
}
public sealed class BookingIdentityUploadUncertainException(string objectPath) : Exception("Identity upload result unknown.")
{
    public string ObjectPath { get; } = objectPath;
}
