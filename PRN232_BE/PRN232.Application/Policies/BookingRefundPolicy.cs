using PRN232.Application.States;

namespace PRN232.Application.Policies;

// P1 policy contract for P6-P10. No provider calls, persistence or role authorization here.
// Future handlers must load these facts from DB under the existing booking/room lock order.
public static class BookingRefundPolicy
{
    public static bool CanRequestRefund(string? bookingStatus, bool hasContract, bool hasSuccessfulDeposit,
        string? latestRefundStatus) =>
        bookingStatus == BookingStatuses.Deposited && !hasContract && hasSuccessfulDeposit &&
        latestRefundStatus is null or BookingRefundRequestStatuses.Rejected;

    public static bool CanCreateContract(string? bookingStatus, bool hasContract, string? latestRefundStatus) =>
        bookingStatus == BookingStatuses.Deposited && !hasContract &&
        latestRefundStatus is null or BookingRefundRequestStatuses.Rejected;

    public static bool CanStartPayout(string? bookingStatus, bool hasContract, string? refundStatus,
        string? latestAttemptStatus) =>
        bookingStatus == BookingStatuses.Deposited && !hasContract &&
        refundStatus == BookingRefundRequestStatuses.Approved &&
        (latestAttemptStatus is null || PayoutAttemptStatuses.CanRetryAsNewAttempt(latestAttemptStatus));

    public static bool CanFinalizeCancellation(string? bookingStatus, bool hasContract, string? refundStatus,
        string? attemptStatus) =>
        bookingStatus == BookingStatuses.Deposited && !hasContract &&
        (refundStatus is BookingRefundRequestStatuses.Refunding or BookingRefundRequestStatuses.RequiresReview) &&
        attemptStatus == PayoutAttemptStatuses.Succeeded;
}
