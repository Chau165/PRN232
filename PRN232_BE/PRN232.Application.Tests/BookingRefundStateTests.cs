using PRN232.Application.Policies;
using PRN232.Application.States;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingRefundStateTests
{
    [Fact]
    public void Approval_submission_and_success_are_three_different_events()
    {
        Assert.True(BookingRefundRequestStatuses.CanTransition("PendingReview", "Approved"));
        Assert.False(BookingRefundRequestStatuses.CanTransition("PendingReview", "Refunded"));
        Assert.False(BookingRefundRequestStatuses.CanTransition("Approved", "Refunded"));
        Assert.True(BookingRefundPolicy.CanStartPayout("Deposited", false, "Approved", null));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Approved", null));

        Assert.True(BookingRefundRequestStatuses.CanTransition("Approved", "Refunding"));
        Assert.True(PayoutAttemptStatuses.CanTransition("Creating", "Processing"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Refunding", "Processing"));
        Assert.True(PayoutAttemptStatuses.CanTransition("Processing", "Succeeded"));
        Assert.True(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Refunding", "Succeeded"));
        Assert.True(BookingRefundRequestStatuses.CanTransition("Refunding", "Refunded"));
    }

    [Theory]
    [InlineData("PendingReview")]
    [InlineData("Approved")]
    [InlineData("Refunding")]
    [InlineData("RequiresReview")]
    public void An_active_refund_blocks_another_refund_and_contract(string status)
    {
        Assert.True(BookingRefundRequestStatuses.IsActive(status));
        Assert.False(BookingRefundPolicy.CanRequestRefund("Deposited", false, true, status));
        Assert.False(BookingRefundPolicy.CanCreateContract("Deposited", false, status));
    }

    [Fact]
    public void Rejection_keeps_the_deposit_and_allows_a_new_request_or_contract()
    {
        Assert.True(BookingRefundRequestStatuses.CanTransition("PendingReview", "Rejected"));
        Assert.False(BookingRefundRequestStatuses.CanTransition("Rejected", "PendingReview"));
        Assert.True(BookingRefundPolicy.CanRequestRefund("Deposited", false, true, "Rejected"));
        Assert.True(BookingRefundPolicy.CanCreateContract("Deposited", false, "Rejected"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Rejected", null));
    }

    [Theory]
    [InlineData("Pending", false, true)]
    [InlineData("Cancelled", false, true)]
    [InlineData("Deposited", true, true)]
    [InlineData("Deposited", false, false)]
    [InlineData("Unknown", false, true)]
    public void Only_a_real_deposit_without_a_contract_can_be_requested(string status, bool contract, bool paid)
    {
        Assert.False(BookingRefundPolicy.CanRequestRefund(status, contract, paid, null));
    }

    [Theory]
    [InlineData("Creating")]
    [InlineData("Processing")]
    [InlineData("RequiresReview")]
    [InlineData("Succeeded")]
    [InlineData("Unknown")]
    [InlineData("Failed")]
    public void Uncertain_successful_or_unknown_attempts_cannot_spawn_another_transfer(string status)
    {
        Assert.False(PayoutAttemptStatuses.CanRetryAsNewAttempt(status));
        Assert.False(BookingRefundPolicy.CanStartPayout("Deposited", false, "Approved", status));
    }

    [Theory]
    [InlineData("Create-failed")]
    [InlineData("Transfer-failed")]
    public void A_confirmed_failure_allows_a_new_attempt_but_never_reopens_the_old_attempt(string status)
    {
        Assert.True(PayoutAttemptStatuses.IsTerminal(status));
        Assert.True(PayoutAttemptStatuses.CanRetryAsNewAttempt(status));
        Assert.False(PayoutAttemptStatuses.CanTransition(status, "Creating"));
        Assert.True(BookingRefundPolicy.CanStartPayout("Deposited", false, "Approved", status));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Refunding", status));
    }

    [Fact]
    public void Timeout_and_provider_review_do_not_release_the_room_or_allow_a_second_payout()
    {
        Assert.True(PayoutAttemptStatuses.IsActive("Creating"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "Refunding", "Creating"));
        Assert.True(PayoutAttemptStatuses.CanTransition("Processing", "RequiresReview"));
        Assert.True(BookingRefundRequestStatuses.CanTransition("Refunding", "RequiresReview"));
        Assert.False(BookingRefundPolicy.CanStartPayout("Deposited", false, "RequiresReview", "RequiresReview"));
        Assert.True(PayoutAttemptStatuses.CanTransition("RequiresReview", "Succeeded"));
        Assert.True(BookingRefundPolicy.CanFinalizeCancellation("Deposited", false, "RequiresReview", "Succeeded"));
    }

    [Theory]
    [InlineData("Refunded")]
    [InlineData("Unknown")]
    public void A_completed_or_unknown_refund_does_not_accidentally_unlock_a_deposited_booking(string status)
    {
        Assert.False(BookingRefundPolicy.CanRequestRefund("Deposited", false, true, status));
        Assert.False(BookingRefundPolicy.CanCreateContract("Deposited", false, status));
    }

    [Fact]
    public void Success_is_terminal_and_cancellation_requires_all_consistent_facts()
    {
        Assert.True(PayoutAttemptStatuses.IsTerminal("Succeeded"));
        Assert.False(PayoutAttemptStatuses.CanTransition("Succeeded", "Transfer-failed"));
        Assert.False(PayoutAttemptStatuses.CanTransition("Succeeded", "Creating"));
        Assert.True(BookingRefundRequestStatuses.IsTerminal("Refunded"));
        Assert.False(BookingRefundRequestStatuses.CanTransition("Refunded", "Approved"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Deposited", true, "Refunding", "Succeeded"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Pending", false, "Refunding", "Succeeded"));
        Assert.False(BookingRefundPolicy.CanFinalizeCancellation("Cancelled", false, "Refunded", "Succeeded"));
    }

    [Fact]
    public void Submission_requires_approval_and_a_matching_booking_state()
    {
        Assert.True(BookingRefundRequestStatuses.CanTransition("Approved", "RequiresReview"));
        Assert.True(BookingRefundPolicy.CanRequestRefund("Deposited", false, true, null));
        Assert.False(BookingRefundPolicy.CanStartPayout("Deposited", false, "PendingReview", null));
        Assert.False(BookingRefundPolicy.CanStartPayout("Deposited", true, "Approved", null));
        Assert.False(BookingRefundPolicy.CanStartPayout("Cancelled", false, "Approved", null));
        Assert.False(BookingRefundRequestStatuses.CanTransition("Refunding", "Rejected"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public void Unknown_states_fail_closed(string? status)
    {
        Assert.False(BookingRefundRequestStatuses.IsValid(status));
        Assert.False(PayoutAttemptStatuses.IsValid(status));
        Assert.False(BookingRefundRequestStatuses.CanTransition(status, "Approved"));
        Assert.False(PayoutAttemptStatuses.CanTransition(status, "Succeeded"));
    }
}
