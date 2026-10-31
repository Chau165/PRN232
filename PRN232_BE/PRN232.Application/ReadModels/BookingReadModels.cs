namespace PRN232.Application.ReadModels;

public sealed record BookingRoomReadModel(int RoomId, decimal MonthlyRent);

public sealed record BookingPaymentReadModel(
    int BookingId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    decimal DepositAmount,
    string Status,
    DateTime ExpiryDate);

public sealed record BookingReadModel(
    int BookingId,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    decimal DepositAmount,
    string Status,
    DateTime ExpiryDate,
    string CustomerPhone,
    int? ContractId);
