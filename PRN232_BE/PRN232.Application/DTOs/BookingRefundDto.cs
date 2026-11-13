namespace PRN232.Application.DTOs;

// Amount, customer, original payment and status are always resolved by the server.
public sealed record CreateBookingRefundDto(string Reason, string RecipientBankBin,
    string RecipientAccountNumber, string? RecipientAccountName = null);

public sealed record BookingRefundDto(int BookingRefundRequestId, int BookingId, int OriginalPaymentId,
    decimal RequestedAmount, string Currency, string Status, string Reason, string RecipientBankBin,
    string MaskedRecipientAccountNumber, string? RecipientAccountName, bool AccountNameVerified,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
