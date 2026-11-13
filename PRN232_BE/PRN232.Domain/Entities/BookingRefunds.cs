namespace PRN232.Domain.Entities;

// Positive outbound amount, separate from the original incoming Payments receipt.
// Written once when business finalization commits; there is no edit/delete API.
public class BookingRefunds
{
    public int BookingRefundId { get; set; }
    public int BookingRefundRequestId { get; set; }
    public int PayoutAttemptId { get; set; }
    public int OriginalPaymentId { get; set; }
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime RecordedAtUtc { get; set; }
    public DateTime? TransferredAtUtc { get; set; }

    public BookingRefundRequests? BookingRefundRequest { get; set; }
    public PayoutAttempts? PayoutAttempt { get; set; }
    public Payments? OriginalPayment { get; set; }
    public Bookings? Booking { get; set; }
    public Users? Customer { get; set; }
}
