using PRN232.Domain.Entities;

namespace PRN232.Application.ReadModels;

public sealed record ContractBookingReadModel(
    int BookingId,
    int? RoomId,
    string RoomCode,
    int CustomerId,
    int OwnerId,
    string Status,
    decimal DepositAmount,
    decimal? MonthlyRent,
    bool HasContract,
    bool HasSuccessfulPayment = false,
    string? LessorName = null,
    string? LessorAddress = null,
    int PropertyId = 0,
    string CustomerName = "",
    int? BookingOccupantCount = null,
    int MaxOccupants = 1,
    int RoomTypeId = 0,
    string RoomTypeName = "");

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
    string? ProcessedByStaffName,
    decimal PrepaidRentAmount = 0m,
    decimal PrepaidRentApplied = 0m,
    decimal RemainingPrepaidCredit = 0m,
    string? PropertyAddress = null,
    int? LessorUserId = null,
    string? LessorName = null,
    string? LessorAddress = null,
    decimal? InitialElectricReading = null,
    decimal? InitialWaterReading = null,
    DateOnly? MeterHandoverDate = null,
    bool HasUtilityReadings = false,
    bool HasInvoices = false,
    decimal SecurityDepositAmount = 0m,
    DateOnly? PlannedMoveOutDate = null,
    DateTime? MoveOutNoticeAtUtc = null,
    int? TransferredFromContractId = null,
    int? TransferredToContractId = null,
    int? RegisteredVehicleCount = null,
    decimal? ManualSecurityDepositAmount = null,
    DateTime? SecurityDepositAcknowledgedAtUtc = null,
    int? SecurityDepositRecordedByManagerId = null);

public sealed record ContractCreationCommand(
    Contracts Contract,
    IReadOnlyList<ContractOccupants> Occupants,
    IdentityDocuments PendingHolderIdentityDocument,
    bool OccupantCountChangeConfirmed,
    bool CustomerRoomSelectionAcknowledged = false,
    DateTime? RoomSelectionConfirmedAtUtc = null);

public enum ContractCreationStatus
{
    Created,
    Conflict,
    HolderIdentityConflict
}
