namespace PRN232.Application.DTOs;

public record PaymentReconciliationDto(int PaymentRequestId, string Result, string Status, DateTime CheckedAtUtc);
