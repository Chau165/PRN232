namespace PRN232.Application.DTOs;

public record PaymentRequestStatusDto(
    int PaymentRequestId, int InvoiceId, int CustomerId, int PropertyId,
    string Status, string InvoiceStatus, decimal Amount, string Currency,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ExpiresAtUtc,
    DateTime? ConfirmedAtUtc, int? PaymentId, DateTime? PaidAtUtc,
    bool IsExpired, bool CanOpenCheckout, string? CheckoutUrl);
