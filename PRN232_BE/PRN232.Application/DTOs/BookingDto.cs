namespace PRN232.Application.DTOs;

public record BookingDto(
    int BookingId,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    decimal DepositAmount,
    string Status,
    DateTime ExpiryDate);

public record CreateBookingDto(int RoomId);

public record BookingRoomDto(int RoomId, decimal MonthlyRent);
