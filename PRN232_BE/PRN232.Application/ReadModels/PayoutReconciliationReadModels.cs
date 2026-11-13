namespace PRN232.Application.ReadModels;

public sealed record PayoutLookupRequest(string ReferenceId, string? ProviderPayoutId);
public sealed record PayoutRemoteSnapshot(string ProviderPayoutId, string ReferenceId, string ProviderTransactionId,
    string TransactionReferenceId, decimal Amount, string RecipientBankBin, string RecipientAccountNumber,
    string ApprovalState, string TransactionState, string? BankReference = null, string? TransactionDatetime = null,
    string? ErrorCode = null);

public enum PayoutReconciliationOutcome { Processing, Succeeded, Failed, RequiresReview, Retry }
public sealed record PayoutReconciliationResult(PayoutReconciliationOutcome Outcome, string? ErrorCode = null,
    PayoutRemoteSnapshot? Remote = null, DateTime? TransferredAtUtc = null);

// LastCheckedAtUtc is also a short DB lease. HTTP is always outside the transaction.
public sealed record PayoutReconciliationWindow(DateTime NowUtc, DateTime DueBeforeUtc, DateTime StartedBeforeUtc,
    DateTime RetryBeforeUtc, bool RetryFailedPayouts, int MaxPayoutAttempts);
