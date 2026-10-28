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

public record PayBookingDto(string PaymentMethod);

public record BookingPaymentDto(
    int BookingId,
    int CustomerId,
    int OwnerId,
    string RoomCode,
    decimal DepositAmount,
    string Status,
    DateTime ExpiryDate);
