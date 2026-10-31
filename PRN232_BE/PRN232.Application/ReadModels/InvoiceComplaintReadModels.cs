namespace PRN232.Application.ReadModels;

public sealed record ComplaintInvoiceReadModel(
    int InvoiceId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    DateOnly InvoiceMonth,
    IReadOnlyList<string> LineDescriptions);

public sealed record InvoiceComplaintReadModel(
    int ComplaintId,
    int InvoiceId,
    DateOnly InvoiceMonth,
    string RoomCode,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    string FeeType,
    string Content,
    string Status,
    DateTime CreatedAt);
