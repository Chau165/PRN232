namespace PRN232.Application.DTOs;

// OutstandingDebtAcknowledged: bắt buộc true khi khách còn nợ hóa đơn (cọc còn lại được giữ để bù nợ).
public record SettleDepositDto(bool OutstandingDebtAcknowledged = false, string? Note = null);

// IsSettled = false là bản xem trước (chưa lưu). Cọc được chia hết vào 4 phần:
// DamageCoveredByDeposit + RetainedForDebt + ForfeitedAmount + RefundAmount = DepositAmount.
// AmountDue = phần bồi thường vượt quá cọc, khách nộp thêm.
public record DepositSettlementDto(
    int ContractId,
    bool IsSettled,
    DateOnly MoveOutDate,
    DateOnly ContractEndDate,
    decimal DepositAmount,
    decimal DamageCharges,
    decimal DamageCoveredByDeposit,
    decimal OutstandingDebt,
    decimal RetainedForDebt,
    decimal ForfeitedAmount,
    decimal RefundAmount,
    decimal AmountDue,
    bool EarlyWithoutNotice,
    int? NoticeDays,
    string? Note,
    string? SettledByName,
    DateTime? SettledAtUtc,
    DateTime? RefundConfirmedAtUtc,
    string? RefundConfirmedByName,
    DateTime? DueCollectedAtUtc,
    string? DueCollectedByName);
