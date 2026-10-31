namespace PRN232.Application.ReadModels;

public sealed record IdentityDocumentReadModel(
    int IdentityDocumentId,
    int CustomerId,
    string CustomerName,
    string CCCDNumber,
    string Status,
    int? ReviewedBy,
    string? ReviewedByName);
