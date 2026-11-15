namespace PRN232.Application.ReadModels;

public sealed record HandoverContractReadModel(
    int ContractId, int RoomId, int PropertyId, int CustomerId, string Status, DateOnly StartDate);

public sealed record HandoverEquipmentReadModel(int EquipmentId, string Name, int Quantity, decimal CompensationValue);

public sealed record HandoverItemReadModel(
    int HandoverItemId, int? EquipmentId, string EquipmentName, int Quantity, decimal CompensationValue,
    string Condition, decimal ChargeAmount, string? Note);

public sealed record HandoverRecordReadModel(
    int HandoverRecordId, string Kind, DateOnly HandoverDate, string? Note, string RecordedByName,
    DateTime RecordedAtUtc, IReadOnlyList<HandoverItemReadModel> Items);

public sealed record ContractEvidenceFileReadModel(
    int ContractEvidenceFileId, string Kind, string ObjectPath, string ContentType, long SizeBytes, string FileName,
    int UploadedById, string UploadedByName, string UploadedByRole, DateTime UploadedAtUtc,
    DateTime? HiddenAtUtc, string? HiddenReason);

public sealed record DepositSettlementReadModel(
    int ContractId, DateOnly MoveOutDate, decimal DepositAmount, decimal DamageCharges, decimal OutstandingDebt,
    decimal RetainedForDebt, decimal ForfeitedAmount, decimal RefundAmount, decimal AmountDue, bool EarlyWithoutNotice,
    int? NoticeDays, string? Note, string SettledByName, DateTime SettledAtUtc,
    DateTime? RefundConfirmedAtUtc, string? RefundConfirmedByName, DateTime? DueCollectedAtUtc, string? DueCollectedByName);

// Dòng thiết bị đã được service kiểm tra và chụp lại giá trị, sẵn sàng ghi.
public sealed record HandoverItemWriteModel(
    int EquipmentId, string EquipmentName, int Quantity, decimal CompensationValue,
    string Condition, decimal ChargeAmount, string? Note);
