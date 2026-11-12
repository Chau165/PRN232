namespace PRN232.Domain.Entities;

// Business request to cancel a deposited booking and refund its original payment.
// This record is not evidence that a bank transfer was made.
public class BookingRefundRequests
{
    public int BookingRefundRequestId { get; set; }
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public int OriginalPaymentId { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingReview";
    // Customer-supplied destination, verified/frozen during approval in P6/P7.
    // Sensitive data: do not log or return these entities directly from APIs.
    public string RecipientBankBin { get; set; } = string.Empty;
    public string RecipientAccountNumber { get; set; } = string.Empty;
    public string? RecipientAccountName { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }

    public Bookings? Booking { get; set; }
    public Users? Customer { get; set; }
    public Payments? OriginalPayment { get; set; }
    public Users? ReviewedByAdmin { get; set; }
    public ICollection<PayoutAttempts> PayoutAttempts { get; set; } = new List<PayoutAttempts>();
}
