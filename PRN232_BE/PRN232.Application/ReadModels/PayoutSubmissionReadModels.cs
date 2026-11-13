namespace PRN232.Application.ReadModels;

public sealed record PayoutSubmissionRequest(string ReferenceId, string IdempotencyKey, decimal Amount,
    string RecipientBankBin, string RecipientAccountNumber, string Description);

// Acceptance is not confirmation of receipt of money. P9/P10 reconcile and finalize it.
public sealed record PayoutSubmissionReceipt(string ProviderPayoutId, string? ProviderTransactionId,
    string ApprovalState, string TransactionState);

public enum PayoutDispatchOutcome { Accepted, NotSent, Unknown }
