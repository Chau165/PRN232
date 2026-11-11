namespace PRN232.Application.ReadModels;

public record PaymentRequestStatusReadModel(
    int PaymentRequestId, int? InvoiceId, int CustomerId, int PropertyId,
    string Status, string? InvoiceStatus, decimal Amount, string Currency,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ExpiresAtUtc,
    DateTime? ConfirmedAtUtc, int? PaymentId, DateTime? PaidAtUtc, string? CheckoutUrl,
    int? BookingId = null, string? BookingStatus = null, bool BookingHasContract = false);
