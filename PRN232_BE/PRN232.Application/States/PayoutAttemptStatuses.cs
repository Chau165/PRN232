namespace PRN232.Application.States;

// One outbound attempt. A retry after a definitive failure creates another attempt;
// it never rewrites the original attempt or changes its reference/idempotency key.
public static class PayoutAttemptStatuses
{
    public const string Creating = "Creating";
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string CreateFailed = "Create-failed";
    public const string TransferFailed = "Transfer-failed";
    public const string RequiresReview = "RequiresReview";

    public static bool IsValid(string? status) => status is Creating or Processing or Succeeded or CreateFailed or TransferFailed or RequiresReview;
    public static bool IsActive(string? status) => status is Creating or Processing or RequiresReview;
    public static bool IsTerminal(string? status) => status is Succeeded or CreateFailed or TransferFailed;
    public static bool CanRetryAsNewAttempt(string? status) => status is CreateFailed or TransferFailed;

    // A timeout preserves Creating/Processing; it is not evidence for either failure state.
    // These edges describe possible outcomes. Provider evidence must be verified by the future handler.
    public static bool CanTransition(string? from, string? to) => (from, to) switch
    {
        (Creating, Processing or Succeeded or CreateFailed or TransferFailed or RequiresReview) => true,
        (Processing, Succeeded or TransferFailed or RequiresReview) => true,
        (RequiresReview, Processing or Succeeded or CreateFailed or TransferFailed) => true,
        _ => false
    };
}
