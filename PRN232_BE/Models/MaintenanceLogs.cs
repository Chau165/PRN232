namespace PRN232_BE.Models;

public class MaintenanceLogs
{
    public int LogId { get; set; }
    public int RequestId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public int? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }

    public MaintenanceRequests? Request { get; set; }
    public Users? ChangedByUser { get; set; }
}
