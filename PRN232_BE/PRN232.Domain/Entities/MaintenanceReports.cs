namespace PRN232.Domain.Entities;

public class MaintenanceReports
{
    public int ReportId { get; set; }
    public int RequestId { get; set; }
    public int ReportedBy { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public MaintenanceRequests? Request { get; set; }
    public Users? Reporter { get; set; }
    public ICollection<MaintenanceReportImages> Images { get; set; } = new List<MaintenanceReportImages>();
}
