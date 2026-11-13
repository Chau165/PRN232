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
    bool HasInvoices = false)
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

public record CreateContractDto(int BookingId, DateOnly StartDate, DateOnly EndDate);

public record ExtendContractRequestDto(DateOnly NewEndDate);

public record TerminateContractRequestDto(
    bool CheckoutConfirmed,
    bool RoomReadyForRental,
    bool OutstandingDebtAcknowledged);

public record TerminateContractResponseDto(ContractDto Contract, decimal OutstandingDebt);
