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
    string? ProcessedByStaffName,
    decimal PrepaidRentAmount = 0m,
    decimal PrepaidRentApplied = 0m,
    decimal RemainingPrepaidCredit = 0m,
    string? PropertyAddress = null,
    int? LessorUserId = null,
    string? LessorName = null,
    string? LessorAddress = null,
    string DisplayStatus = "Upcoming",
    decimal? InitialElectricReading = null,
    decimal? InitialWaterReading = null,
    DateOnly? MeterHandoverDate = null,
    bool HasUtilityReadings = false,
    bool HasInvoices = false,
    // Rebuild 3.3: tiền cọc đang giữ làm tiền đảm bảo (hoàn khi trả phòng). 0 với hợp đồng theo luật cũ
    // (cọc đã/sẽ trừ vào tháng đầu, xem PrepaidRent*).
    decimal SecurityDepositAmount = 0m,
    // Rebuild 3.6a: báo trả phòng. MoveOutNoticeDays = số ngày từ lúc báo (giờ VN) đến ngày dự kiến trả.
    // MoveOutForfeitsDeposit = true khi trả trước ngày kết thúc mà báo trước dưới 30 ngày (FE cảnh báo khách).
    DateOnly? PlannedMoveOutDate = null,
    DateTime? MoveOutNoticeAtUtc = null,
    int? MoveOutNoticeDays = null,
    bool MoveOutForfeitsDeposit = false,
    // Rebuild 4.2: hợp đồng này nhận chuyển từ / đã chuyển sang hợp đồng nào (chuyển người đứng tên).
    int? TransferredFromContractId = null,
    int? TransferredToContractId = null,
    int? RegisteredVehicleCount = null,
    decimal? ManualSecurityDepositAmount = null,
    DateTime? SecurityDepositAcknowledgedAtUtc = null)
{
    public bool HasMeterHandover => InitialElectricReading.HasValue &&
        InitialWaterReading.HasValue && MeterHandoverDate.HasValue;
}

public record ContractMeterHandoverValues(
    DateOnly MeterHandoverDate,
    decimal InitialElectricReading,
    decimal InitialWaterReading);

public record SaveContractMeterHandoverDto(
    [System.ComponentModel.DataAnnotations.Required] DateOnly? MeterHandoverDate,
    [System.ComponentModel.DataAnnotations.Required] decimal? InitialElectricReading,
    [System.ComponentModel.DataAnnotations.Required] decimal? InitialWaterReading,
    ContractMeterHandoverValues? ExpectedMeterHandover = null);

public record CreateContractDto(
    int BookingId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? HolderIdentityNumber = null,
    IReadOnlyList<CreateContractOccupantDto>? Occupants = null,
    int RegisteredVehicleCount = 0,
    decimal ManualSecurityDepositAmount = 0m,
    bool SecurityDepositHandoverAcknowledged = false,
    bool OccupantCountChangeConfirmed = false, Guid? BookingApplicationVersion = null);

public record CreateContractOccupantDto(string? FullName, string? IdentityNumber, string? PhoneNumber = null);

public record UpdateContractRegisteredVehicleCountDto(int RegisteredVehicleCount);

public record ExtendContractRequestDto(DateOnly NewEndDate);

// Rebuild 3.6a: khách hoặc quản lý báo ngày dự kiến trả phòng.
public record MoveOutNoticeDto(DateOnly PlannedMoveOutDate);

public record TerminateContractRequestDto(
    bool CheckoutConfirmed,
    bool RoomReadyForRental,
    bool OutstandingDebtAcknowledged);

public record TerminateContractResponseDto(ContractDto Contract, decimal OutstandingDebt);
