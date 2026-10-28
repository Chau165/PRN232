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

public record InvoiceContractDto(
    int ContractId,
    int OwnerId,
    int CustomerId,
    string CustomerName,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    int RoomTypeId,
    decimal MonthlyRent,
    string Status,
    DateOnly StartDate,
    DateOnly EndDate);

// Một dịch vụ của loại phòng kèm biểu giá đang hiệu lực trong tháng (null nếu khu trọ chưa đặt giá).
public record BillableServiceDto(
    int ServiceId,
    string ServiceName,
    int? RateId,
    decimal? Price,
    string? UnitCode,
    string? BillingBasis);

public record MonthUsageDto(decimal ElectricUsage, decimal WaterUsage);

public record PayInvoiceDto(string PaymentMethod);

public record InvoicePaymentDto(
    int InvoiceId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    DateOnly InvoiceMonth,
    string Status,
    decimal Total);
