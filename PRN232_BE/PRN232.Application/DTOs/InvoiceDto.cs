namespace PRN232.Application.DTOs;

// Bản xem trước có InvoiceId = 0 và Status = "Preview".
public record InvoiceDto(
    int InvoiceId,
    int ContractId,
    string RoomCode,
    string PropertyName,
    string CustomerName,
    DateOnly InvoiceMonth,
    DateOnly DueDate,
    string Status,
    decimal Total,
    IReadOnlyList<InvoiceLineDto> Lines);

public record InvoiceLineDto(
    string ItemType,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    string? UnitCode,
    decimal Amount);

public record CreateInvoiceDto(int ContractId, DateOnly InvoiceMonth, DateOnly DueDate);

public record PayInvoiceDto(string PaymentMethod);
