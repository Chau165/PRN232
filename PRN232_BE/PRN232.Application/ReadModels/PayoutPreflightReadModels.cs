namespace PRN232.Application.ReadModels;

public record PayoutPreflightRequest(string ReferenceId, decimal Amount, string RecipientBankBin,
    string RecipientAccountNumber, string Description);

public record PayoutBalanceSnapshot(decimal Balance, string Currency, DateTime CheckedAtUtc);

// Keep provider credit units explicit; do not assume credits are deducted from the VND principal balance.
public record PayoutCostEstimate(long EstimatedCredit, bool DestinationValidationRequested, DateTime CheckedAtUtc);

// Estimate-credit does not return an account holder name or a destination-verification result.
// This is not authorization, a balance reservation, or permission to issue a transfer.
public record PayoutPreflightSnapshot(PayoutBalanceSnapshot Balance, PayoutCostEstimate Cost,
    bool HasSufficientPrincipalBalance, bool AccountNameVerified = false, string? RecipientAccountName = null);
