namespace PRN232.Application.Settings;

// Outbound channel credentials. Never fall back to incoming PayOS settings or log this object.
public class PayOSPayoutSettings
{
    public const string SectionName = "PayOSPayout";
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 15000;
    public bool ValidateDestinationOnEstimate { get; set; } = true;
}
