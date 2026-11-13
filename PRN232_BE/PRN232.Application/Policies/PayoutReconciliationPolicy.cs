using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

public static class PayoutReconciliationPolicy
{
    private static readonly string[] Approvals = ["Drafting", "Submitted", "Approved", "Rejected", "Cancelled", "Scheduled", "Processing", "Failed", "PartialCompleted", "Completed"];
    private static readonly string[] Transactions = ["Received", "Processing", "Cancelled", "Succeeded", "OnHold", "Reversed", "Failed"];

    public static PayoutReconciliationResult Evaluate(PayoutAttempts attempt, PayoutRemoteSnapshot? remote, DateTime now, int stuckAfterSeconds)
    {
        if (remote is null) return new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_NOT_FOUND");
        if (!Identifier(remote.ProviderPayoutId, 128) || !Identifier(remote.ProviderTransactionId, 128) ||
            remote.ReferenceId != attempt.ReferenceId || remote.TransactionReferenceId != attempt.ReferenceId ||
            remote.Amount != attempt.Amount || attempt.Currency != "VND" || remote.RecipientBankBin != attempt.RecipientBankBin ||
            remote.RecipientAccountNumber != attempt.RecipientAccountNumber ||
            attempt.ProviderPayoutId is not null && attempt.ProviderPayoutId != remote.ProviderPayoutId ||
            attempt.ProviderTransactionId is not null && attempt.ProviderTransactionId != remote.ProviderTransactionId ||
            !Approvals.Contains(remote.ApprovalState) || !Transactions.Contains(remote.TransactionState) ||
            remote.BankReference is { Length: > 256 } || remote.BankReference?.Any(char.IsControl) == true)
            return new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_LOOKUP_MISMATCH");

        DateTime? transferred = null;
        if (!string.IsNullOrWhiteSpace(remote.TransactionDatetime))
        {
            if (!PayOSTransactionTime.TryGetUtc(remote.TransactionDatetime, out var value) || value > now || value < attempt.CreatedAtUtc.AddSeconds(-1))
                return new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_TIME_INVALID", remote);
            // A seconds-only timestamp can lose subsecond precision. Do not invent an actual transfer time.
            if (value >= attempt.CreatedAtUtc) transferred = value;
        }
        if (remote.ApprovalState == "Completed" && remote.TransactionState == "Succeeded" && string.IsNullOrWhiteSpace(remote.ErrorCode))
            return new(PayoutReconciliationOutcome.Succeeded, Remote: remote, TransferredAtUtc: transferred);

        // Deliberately strict: a reversal, missing error evidence or a bank reference is not proof of no transfer.
        if (remote.ApprovalState is "Failed" or "Rejected" or "Cancelled" && remote.TransactionState is "Failed" or "Cancelled" &&
            !string.IsNullOrWhiteSpace(remote.ErrorCode) && string.IsNullOrWhiteSpace(remote.BankReference) &&
            string.IsNullOrWhiteSpace(remote.TransactionDatetime))
            return new(PayoutReconciliationOutcome.Failed, "PAYOUT_REMOTE_FAILED", remote);

        if (remote.ApprovalState is "Drafting" or "Submitted" or "Approved" or "Scheduled" or "Processing" &&
            remote.TransactionState is "Received" or "Processing" && string.IsNullOrWhiteSpace(remote.ErrorCode))
            return (now - attempt.CreatedAtUtc).TotalSeconds >= stuckAfterSeconds
                ? new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_REMOTE_STUCK", remote)
                : new(PayoutReconciliationOutcome.Processing, Remote: remote);
        return new(PayoutReconciliationOutcome.RequiresReview, "PAYOUT_REMOTE_REQUIRES_REVIEW", remote);
    }

    public static bool HasDefinitiveFailure(PayoutAttempts attempt) =>
        attempt.CompletedAtUtc is not null && attempt.TransferredAtUtc is null && string.IsNullOrWhiteSpace(attempt.BankReference) &&
        (attempt.Status == PayoutAttemptStatuses.CreateFailed && attempt.SubmittedAtUtc is null &&
            attempt.ProviderPayoutId is null && attempt.ProviderTransactionId is null &&
            attempt.LastErrorCode is "PAYOUT_INSUFFICIENT_PRINCIPAL" or "PAYOUT_PREFLIGHT_UNAVAILABLE" ||
         attempt.Status == PayoutAttemptStatuses.TransferFailed && attempt.ProviderPayoutId is not null &&
            attempt.ProviderTransactionId is not null && attempt.LastErrorCode == "PAYOUT_REMOTE_FAILED" &&
            attempt.ProviderApprovalState is "Failed" or "Rejected" or "Cancelled" &&
            attempt.ProviderTransactionState is "Failed" or "Cancelled");

    private static bool Identifier(string? value, int length) => !string.IsNullOrWhiteSpace(value) && value.Length <= length &&
        value == value.Trim() && !value.Any(char.IsControl);
}
