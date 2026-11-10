namespace PRN232.Domain.Entities;

// Authenticated provider events retained before settlement (B8). No raw payload/PII.
public class PaymentWebhookReceipts
{
    public int PaymentWebhookReceiptId { get; set; }
    public string Provider { get; set; } = "PayOS";
    public string Reference { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string PaymentLinkId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string DataCode { get; set; } = string.Empty;
    public string? TransactionDateTime { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string? ConflictingFingerprint { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public int? PaymentRequestId { get; set; }
    public PaymentRequests? PaymentRequest { get; set; }
}
