namespace PRN232.Application.ReadModels;

public sealed record TransferUserReadModel(int UserId, string FullName, string Email, string RoleName, bool IsActive);

public sealed record ContractTransferReadModel(
    int ContractTransferId, string Status, int FromContractId, int PropertyId, string RoomCode, string PropertyName,
    int OldHolderId, string OldHolderName, int NewHolderId, string NewHolderName, string NewHolderEmail,
    int? BecomingHolderOccupantId, string? BecomingHolderOccupantName, DateOnly EffectiveDate, string RequestedByName,
    DateTime CreatedAtUtc, DateTime? OldHolderConfirmedAtUtc, DateTime? NewHolderConfirmedAtUtc,
    DateTime? CompletedAtUtc, int? NewContractId, DateTime? CancelledAtUtc, string? CancelReason);

// Những sự thật về hợp đồng cũ cần để quyết định có hoàn tất chuyển được không.
public sealed record ContractTransferFactsReadModel(
    DateOnly? LatestReadingMonth,
    decimal? LatestElectricEnd,
    decimal? LatestWaterEnd,
    bool HasMeterBaseline,
    decimal? InitialElectricReading,
    decimal? InitialWaterReading,
    bool HasReadingFromEffectiveMonth,
    bool HasInvoiceFromEffectiveMonth,
    bool HasLastMonthInvoice,
    bool HasMoveOutRecord,
    bool MoveOutHasDamagedOrMissing,
    bool IsDepositSettled,
    int ActiveOccupants,
    bool BecomingHolderIsActive,
    int RoomMaxOccupants);

// Giá trị đã được service kiểm tra, repository dùng để tạo hợp đồng mới trong một transaction.
public sealed record ContractTransferCompletion(
    int ContractTransferId,
    int ManagerId,
    DateTime CompletedAtUtc,
    decimal DepositCarriedOver,
    decimal? InitialElectricReading,
    decimal? InitialWaterReading);
