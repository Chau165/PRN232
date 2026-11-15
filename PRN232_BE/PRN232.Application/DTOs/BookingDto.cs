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
    DateTime? HoldUntilUtc = null, bool CanReleaseRoom = false, decimal? AgreedMonthlyRent = null,
    DateOnly? MoveInDate = null, int? OccupantCount = null);

// MoveInDate/OccupantCount: không gửi thì BE lấy "hôm nay" (giờ VN) và 1 người (API-CHANGES C9).
public record CreateBookingDto(int RoomId, int? ViewingId = null, DateOnly? MoveInDate = null, int? OccupantCount = null);

public record PayBookingDto(string PaymentMethod);
