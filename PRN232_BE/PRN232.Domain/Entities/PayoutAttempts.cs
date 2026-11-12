namespace PRN232.Domain.Entities;

// One outbound attempt. Retry after a verified failure creates a new record/key;
// uncertain outcomes must be looked up using this attempt's original identifiers.
public class PayoutAttempts
{
    public int PayoutAttemptId { get; set; }
    public int BookingRefundRequestId { get; set; }
    public int AttemptNumber { get; set; }
    public int InitiatedByAdminId { get; set; }
    public string Provider { get; set; } = "PayOS";
    public string ReferenceId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderPayoutId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? BankReference { get; set; }
    public string Status { get; set; } = "Creating";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    // Destination/amount snapshot for this attempt; never copied again on retry of the same HTTP request.
    public string RecipientBankBin { get; set; } = string.Empty;
    public string RecipientAccountNumber { get; set; } = string.Empty;
    public string RecipientAccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? EstimatedFee { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? ProviderApprovalState { get; set; }
    public string? ProviderTransactionState { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
    // Local confirmation time, distinct from the optional actual transfer time supplied by the provider.
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? TransferredAtUtc { get; set; }

    public BookingRefundRequests? BookingRefundRequest { get; set; }
    public Users? InitiatedByAdmin { get; set; }
}
