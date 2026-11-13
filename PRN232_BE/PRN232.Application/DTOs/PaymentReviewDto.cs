namespace PRN232.Application.DTOs;

public record PaymentReviewDto(int Id, int InvoiceId, int PaymentRequestId, string Reason, string Status,
    decimal AdditionalAmount, string Currency, string ProviderReference, string Source, string? TransactionDateTime,
    int ExistingPaymentId, string ExistingPaymentMethod, decimal ExistingAmount, DateTime? ExistingPaidAtUtc,
    DateTime CreatedAtUtc, string? Resolution, string? ResolutionNote, string? EvidenceReference,
    int? ResolvedByAdminId, DateTime? ResolvedAtUtc);

public record ResolvePaymentReviewDto(string Resolution, string Note, string? EvidenceReference);
