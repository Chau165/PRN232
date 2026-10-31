namespace PRN232.Application.DTOs;

public record ContractDto(
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

public record CreateContractDto(int BookingId, DateOnly StartDate, DateOnly EndDate);
