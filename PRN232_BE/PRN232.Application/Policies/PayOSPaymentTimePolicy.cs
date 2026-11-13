namespace PRN232.Application.Policies;

public static class PayOSPaymentTimePolicy
{
    public const string InvoiceReviewCode = "WEBHOOK_TRANSACTION_TIME_INVALID";

    // Application tolerance for a timestamp exactly at the start of the creation minute.
    // Does not widen expiry/future limits or replace the bank's reported PaidAtUtc.
    public static bool IsAfterCreation(DateTime paidAtUtc, DateTime createdAtUtc) =>
        paidAtUtc >= createdAtUtc ||
        (paidAtUtc.Ticks % TimeSpan.TicksPerMinute == 0 &&
         paidAtUtc.Ticks == createdAtUtc.Ticks - createdAtUtc.Ticks % TimeSpan.TicksPerMinute);
}
