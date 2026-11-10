namespace PRN232.Domain.Entities;

// An invoice checkout attempt, not money received. A successful settlement links
// to Payments; cash/legacy payments do not require a PaymentRequest.
public class PaymentRequests
{
    public int PaymentRequestId { get; set; }
    public int InvoiceId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Provider { get; set; } = "PayOS";
    public long OrderCode { get; set; }
    // Null while creation is pending or its result is not yet known.
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string Status { get; set; } = "Creating";
    // All timestamps are UTC and supplied by the application TimeProvider.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? WebhookReceivedAtUtc { get; set; }
    public string? ProviderReference { get; set; }
    // Store sanitized diagnostic information only, never credentials/raw payloads.
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public int? PaymentId { get; set; }

    public Invoices? Invoice { get; set; }
    public Users? Customer { get; set; }
    public Payments? Payment { get; set; }
}
