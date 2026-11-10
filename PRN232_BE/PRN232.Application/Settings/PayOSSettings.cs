namespace PRN232.Application.Settings;

// Not an API DTO: never return or log this object, which contains credentials.
public class PayOSSettings
{
    public const string SectionName = "PayOS";
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}
