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
    DateTime ExpiryDate,
    string CustomerPhone,
    int? ContractId, string? RefundStatus = null, bool CanRequestRefund = false, int? LatestPaymentRequestId = null,
    DateTime? HoldUntilUtc = null, bool CanReleaseRoom = false);

public record CreateBookingDto(int RoomId);

public record PayBookingDto(string PaymentMethod);
