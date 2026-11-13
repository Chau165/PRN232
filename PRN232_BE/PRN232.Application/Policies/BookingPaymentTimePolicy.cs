namespace PRN232.Application.Policies;

public static class BookingPaymentTimePolicy
{
    public const string ReviewCode = "BOOKING_PAYMENT_TIME_REQUIRES_REVIEW";
    public static bool IsReviewCode(string? code) => code is ReviewCode or "BOOKING_PAYMENT_REQUIRES_REVIEW";

    // Narrow application tolerance for provider/bank timestamps observed at minute precision.
    // Preserve the actual provider timestamp; never widen expiry or future-time checks.
    public static bool IsAfterCreation(DateTime paidAtUtc, DateTime createdAtUtc) =>
        PayOSPaymentTimePolicy.IsAfterCreation(paidAtUtc, createdAtUtc);
}
