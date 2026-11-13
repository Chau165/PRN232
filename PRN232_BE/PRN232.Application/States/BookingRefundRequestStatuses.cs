namespace PRN232.Application.States;

// Admin's decision and the business progress of a booking refund, not a bank transaction.
public static class BookingRefundRequestStatuses
{
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Refunding = "Refunding";
    public const string Refunded = "Refunded";
    public const string Rejected = "Rejected";
    public const string RequiresReview = "RequiresReview";

    public static bool IsValid(string? status) => status is PendingReview or Approved or Refunding or Refunded or Rejected or RequiresReview;
    public static bool IsActive(string? status) => status is PendingReview or Approved or Refunding or RequiresReview;
    public static bool IsTerminal(string? status) => status is Refunded or Rejected;

    // Repeated observations of the same state are handled as idempotent no-ops by callers.
    // Returning to Approved requires definitive evidence that the previous payout transferred no money.
    public static bool CanTransition(string? from, string? to) => (from, to) switch
    {
        (PendingReview, Approved or Rejected) => true,
        (Approved, Refunding) => true,
        (Refunding, Refunded or Approved or RequiresReview) => true,
        (RequiresReview, Refunding or Refunded or Approved) => true,
        _ => false
    };
}
