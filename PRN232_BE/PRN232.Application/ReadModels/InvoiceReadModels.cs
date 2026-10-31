namespace PRN232.Application.ReadModels;

public sealed record InvoiceContractReadModel(
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

public sealed record BillableServiceReadModel(
    int ServiceId,
    string ServiceName,
    int? RateId,
    decimal? Price,
    string? UnitCode,
    string? BillingBasis);

public sealed record MonthUsageReadModel(decimal ElectricUsage, decimal WaterUsage);

public sealed record InvoicePaymentReadModel(
    int InvoiceId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    DateOnly InvoiceMonth,
    string Status,
    decimal Total);

public sealed record InvoiceLineReadModel(
    string ItemType,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    string? UnitCode,
    decimal Amount);

public sealed record InvoiceReadModel(
    int InvoiceId,
    int ContractId,
    string RoomCode,
    string PropertyName,
    string CustomerName,
    DateOnly InvoiceMonth,
    DateOnly DueDate,
    string Status,
    decimal Total,
    IReadOnlyList<InvoiceLineReadModel> Lines);
