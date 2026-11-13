namespace PRN232.Domain.Entities;

public class PaymentReviewCases
{
    public int PaymentReviewCaseId { get; set; }
    public int PaymentWebhookReceiptId { get; set; }
    public PaymentWebhookReceipts? Receipt { get; set; }
    public int InvoiceId { get; set; }
    public Invoices? Invoice { get; set; }
    public int ExistingPaymentId { get; set; }
    public Payments? ExistingPayment { get; set; }
    public string Reason { get; set; } = "PossibleDuplicatePayment";
    public string Status { get; set; } = "Open";
    public DateTime CreatedAtUtc { get; set; }
    public string? Resolution { get; set; }
    public string? ResolutionNote { get; set; }
    public string? EvidenceReference { get; set; }
    public int? ResolvedByAdminId { get; set; }
    public Users? ResolvedByAdmin { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
