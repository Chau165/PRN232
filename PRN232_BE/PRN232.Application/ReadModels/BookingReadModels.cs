namespace PRN232.Application.ReadModels;

public sealed record BookingRoomReadModel(int? RoomId, string RoomCode, int OwnerId, decimal MonthlyRent, int MaxOccupants = 1, int PropertyId = 0, int RoomTypeId = 0);

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
    int? RoomId,
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
    DateTime? DepositPaidAtUtc = null,
    decimal? AgreedMonthlyRent = null,
    DateOnly? MoveInDate = null,
    int? OccupantCount = null,
    string? CancellationReason = null,
    DateTime? CancelledAtUtc = null,
    bool DepositPaid = false,
    DateTime? DepositReturnedAtUtc = null,
    string? DepositReturnedByName = null,
    bool DepositAppliesToFirstMonth = false,
    int MaxOccupants = 1,
    int RoomTypeId = 0,
    string RoomTypeName = "");

// Người khách liên hệ để nhận lại tiền cọc (hệ thống không chuyển tiền hoàn). UserId không trả ra API,
// chỉ dùng để gửi thông báo cho đúng người.
public sealed record BookingContactReadModel(int PropertyId, string FullName, string PhoneNumber, string Email, string Role,
    int UserId = 0);

// StaffIds: quản lý đang phụ trách khu trọ, hoặc chủ trọ khi khu chưa có quản lý (giống BookingContact).
public sealed record BookingMoveInCandidateReadModel(
    int BookingId, int CustomerId, string CustomerName, string RoomCode, DateOnly MoveInDate, IReadOnlyList<int> StaffIds);

public sealed record ViewingBookingReadModel(
    int ViewingId,
    int? RoomId,
    int CustomerId,
    int PropertyId,
    int RoomTypeId,
    string Status);
