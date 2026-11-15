namespace PRN232.Application.DTOs;

// Rebuild 4.2. EffectiveDate phải là ngày 1 của tháng. OccupantId: người ở cùng của hợp đồng cũ sẽ đứng tên
// (để chuyển họ khỏi danh sách người ở cùng); bỏ trống nếu người nhận chưa có trong danh sách.
public record CreateContractTransferDto(int ContractId, string NewHolderEmail, DateOnly EffectiveDate, int? OccupantId = null);

public record CancelContractTransferDto(string? Reason = null);

// DepositCarriedOver: số cọc sẽ chuyển nguyên sang hợp đồng mới. Blockers: các điều kiện chưa đạt để hoàn tất
// (chỉ trả cho quản lý/Admin; khách nhận danh sách rỗng vì có thể chứa công nợ của người khác).
public record ContractTransferDto(
    int ContractTransferId,
    string Status,
    int FromContractId,
    string RoomCode,
    string PropertyName,
    int OldHolderId,
    string OldHolderName,
    int NewHolderId,
    string NewHolderName,
    string NewHolderEmail,
    string? BecomingHolderOccupantName,
    DateOnly EffectiveDate,
    decimal MonthlyRent,
    DateOnly ContractEndDate,
    decimal DepositCarriedOver,
    string RequestedByName,
    DateTime CreatedAtUtc,
    DateTime? OldHolderConfirmedAtUtc,
    DateTime? NewHolderConfirmedAtUtc,
    DateTime? CompletedAtUtc,
    int? NewContractId,
    DateTime? CancelledAtUtc,
    string? CancelReason,
    bool CanConfirm,
    bool CanCancel,
    bool CanComplete,
    IReadOnlyList<string> Blockers);
