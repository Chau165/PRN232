using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class PayoutReconciliationPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
    private static PayoutAttempts Attempt() => new() { ReferenceId = "refund_1", Amount = 2500000, Currency = "VND",
        RecipientBankBin = "970422", RecipientAccountNumber = "00123456", CreatedAtUtc = Now.AddMinutes(-5) };
    private static PayoutRemoteSnapshot Remote(string approval, string state) =>
        new("payout_1", "refund_1", "txn_1", "refund_1", 2500000, "970422", "00123456", approval, state);

    [Theory]
    [InlineData("Processing", "Processing", PayoutReconciliationOutcome.Processing)]
    [InlineData("Scheduled", "Received", PayoutReconciliationOutcome.Processing)]
    [InlineData("Completed", "Succeeded", PayoutReconciliationOutcome.Succeeded)]
    [InlineData("Completed", "Processing", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Processing", "Succeeded", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Completed", "Reversed", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Approved", "OnHold", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("PartialCompleted", "Succeeded", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Failed", "Succeeded", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Unknown", "Succeeded", PayoutReconciliationOutcome.RequiresReview)]
    [InlineData("Completed", "Unknown", PayoutReconciliationOutcome.RequiresReview)]
    public void Only_consistent_provider_states_can_confirm_success_or_processing(string approval, string state, PayoutReconciliationOutcome expected) =>
        Assert.Equal(expected, PayoutReconciliationPolicy.Evaluate(Attempt(), Remote(approval, state), Now, 3600).Outcome);

    [Theory]
    [InlineData("Failed", "Failed")]
    [InlineData("Rejected", "Cancelled")]
    [InlineData("Cancelled", "Cancelled")]
    public void Failure_requires_terminal_states_error_and_no_evidence_of_transfer(string approval, string state)
    {
        var remote = Remote(approval, state) with { ErrorCode = "REJECTED" };
        Assert.Equal(PayoutReconciliationOutcome.Failed, PayoutReconciliationPolicy.Evaluate(Attempt(), remote, Now, 3600).Outcome);
        foreach (var ambiguous in new[] { remote with { ErrorCode = null }, remote with { BankReference = "BANK-123" },
            remote with { TransactionDatetime = Now.AddMinutes(-1).ToString("O") } })
            Assert.Equal(PayoutReconciliationOutcome.RequiresReview, PayoutReconciliationPolicy.Evaluate(Attempt(), ambiguous, Now, 3600).Outcome);
    }

    [Theory]
    [InlineData("2026-11-13T11:00:00Z")]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("not-a-date")]
    public void Invalid_or_impossible_transfer_times_are_not_silently_recorded(string time) =>
        Assert.Equal(PayoutReconciliationOutcome.RequiresReview,
            PayoutReconciliationPolicy.Evaluate(Attempt(), Remote("Completed", "Succeeded") with { TransactionDatetime = time }, Now, 3600).Outcome);

    [Fact]
    public void A_known_provider_or_transaction_id_cannot_be_replaced_by_another_order()
    {
        var attempt = Attempt(); attempt.ProviderPayoutId = "original";
        Assert.Equal("PAYOUT_LOOKUP_MISMATCH", PayoutReconciliationPolicy.Evaluate(attempt, Remote("Completed", "Succeeded"), Now, 3600).ErrorCode);
        attempt.ProviderPayoutId = null; attempt.ProviderTransactionId = "original-txn";
        Assert.Equal("PAYOUT_LOOKUP_MISMATCH", PayoutReconciliationPolicy.Evaluate(attempt, Remote("Completed", "Succeeded"), Now, 3600).ErrorCode);
    }
}
