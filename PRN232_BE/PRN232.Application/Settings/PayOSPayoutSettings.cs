namespace PRN232.Application.Settings;

// Outbound channel credentials. Never fall back to incoming PayOS settings or log this object.
public class PayOSPayoutSettings
{
    public const string SectionName = "PayOSPayout";
    public bool Enabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public int DispatchIntervalSeconds { get; set; } = 30;
    public int DispatchBatchSize { get; set; } = 10;
    public bool ReconciliationEnabled { get; set; }
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationBatchSize { get; set; } = 20;
    public int ReconciliationMinimumAgeSeconds { get; set; } = 180;
    public int ReconciliationStuckAfterSeconds { get; set; } = 3600;
    public bool RetryFailedPayouts { get; set; }
    public int MaxPayoutAttempts { get; set; } = 3;
    public int PayoutRetryDelaySeconds { get; set; } = 300;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 15000;
    public bool ValidateDestinationOnEstimate { get; set; } = true;
}
