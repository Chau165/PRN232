namespace PRN232.Application.DTOs;

public record BookingPaymentLinkDto(int PaymentRequestId, int BookingId, string Status, decimal Amount,
    string Currency, string? CheckoutUrl, DateTime? ExpiresAtUtc, bool Reused);
