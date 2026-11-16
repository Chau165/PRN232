namespace PRN232.Application.Settings;
public sealed class BookingIdentityCleanupSettings
{
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public int? DraftRetentionDays { get; set; }
    public int BatchSize { get; set; } = 20;
    public void Validate()
    {
        if(BatchSize is <1 or >100 || DraftRetentionDays is <1 or >3650 || Enabled && DraftRetentionDays is null)
            throw new InvalidOperationException("BookingIdentityCleanup requires an explicit retention of 1–3650 days and batch size 1–100 before enabling.");
    }
}
