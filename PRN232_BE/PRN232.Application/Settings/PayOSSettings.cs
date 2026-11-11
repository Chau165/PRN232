namespace PRN232.Application.Settings;

// Not an API DTO: never return or log this object, which contains credentials.
public class PayOSSettings
{
    public const string SectionName = "PayOS";
    public bool Enabled { get; set; }
    // Enable booking checkout only after P2 migration and the booking UI are ready.
    public bool BookingPaymentsEnabled { get; set; }
    public bool ReconciliationEnabled { get; set; }
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationMinAgeSeconds { get; set; } = 60;
    public int ReconciliationBatchSize { get; set; } = 20;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}
