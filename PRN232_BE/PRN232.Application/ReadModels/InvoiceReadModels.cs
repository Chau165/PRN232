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
    DateOnly EndDate,
    int? BookingId = null);

public sealed record BillableServiceReadModel(
    int ServiceId,
    string ServiceName,
    int? RateId,
    decimal? Price,
    string? UnitCode,
    string? BillingBasis);

public sealed record MonthUsageReadModel(decimal ElectricUsage, decimal WaterUsage);

public sealed record PrepaidRentCreditReadModel(decimal AvailableAmount);

public sealed record InvoicePaymentReadModel(
    int InvoiceId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    DateOnly InvoiceMonth,
    string Status,
    decimal Total,
    int PropertyId = 0);

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
    IReadOnlyList<InvoiceLineReadModel> Lines,
    decimal PrepaidRentApplied = 0m,
    decimal AmountPaid = 0m,
    string? PaymentMethod = null,
    DateTime? PaymentDateUtc = null)
{
    public decimal NetPayable => Math.Max(0m, Total);
    public decimal RemainingPayable => Math.Max(0m, NetPayable - AmountPaid);
}
