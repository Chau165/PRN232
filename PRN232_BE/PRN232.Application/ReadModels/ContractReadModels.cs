namespace PRN232.Application.ReadModels;

public sealed record ContractBookingReadModel(
    int BookingId,
    int RoomId,
    string RoomCode,
    int CustomerId,
    int OwnerId,
    string Status,
    decimal DepositAmount,
    decimal MonthlyRent,
    bool HasContract);

public sealed record ContractReadModel(
    int ContractId,
    int? BookingId,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    string Status,
    string? ProcessedByStaffName);
