namespace PRN232.Application.DTOs;

public sealed record ReviewBookingRefundDto(bool Approve, decimal? ApprovedAmount, string ReviewNote);
public sealed record BookingRefundReviewDto(int BookingRefundRequestId, int BookingId, int CustomerId,
    int OriginalPaymentId, decimal DepositAmount, decimal RequestedAmount, decimal? ApprovedAmount,
    string Currency, string Status, string Reason, string RecipientBankBin, string RecipientAccountNumber,
    string? RecipientAccountName, bool AccountNameVerified, int? ReviewedByAdminId, string? ReviewNote,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ReviewedAtUtc);
