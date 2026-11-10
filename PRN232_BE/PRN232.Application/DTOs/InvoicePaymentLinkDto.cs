namespace PRN232.Application.DTOs;

public record InvoicePaymentLinkDto(int PaymentRequestId, int InvoiceId, string Status,
    decimal Amount, string Currency, string? CheckoutUrl, DateTime? ExpiresAtUtc, bool Reused);
