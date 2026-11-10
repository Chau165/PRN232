namespace PRN232.Application.States;

// Separate from PaymentStatuses.Success, used for actual money received.
public static class PaymentRequestStatuses
{
    public const string Creating = "Creating";
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string CreateFailed = "Create-failed";
    public const string RequiresReview = "RequiresReview";

    public static bool IsValid(string? status) => status is
        Creating or Pending or Succeeded or Cancelled or Expired or CreateFailed or RequiresReview;
}
