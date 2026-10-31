namespace PRN232.Domain.Entities;

public class MaintenanceReportImages
{
    public int ImageId
    {
        get; set;
    }
    public int ReportId
    {
        get; set;
    }
    public string ImageUrl { get; set; } = string.Empty;

    public MaintenanceReports? Report
    {
        get; set;
    }
}
