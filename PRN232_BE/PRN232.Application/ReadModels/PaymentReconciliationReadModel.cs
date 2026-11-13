namespace PRN232.Application.ReadModels;

public record PaymentReconciliationReadModel(int PaymentRequestId, long OrderCode, int OwnerId,
    string Status, string? LastErrorCode, DateTime UpdatedAtUtc, int PropertyId = 0);
