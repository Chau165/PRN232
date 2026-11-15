namespace PRN232.Domain.Entities;

// Rebuild 3.6b (PLAN.md D3 bước 6): quyết toán cọc khi trả phòng, mỗi hợp đồng một lần. Lưu lại từng con số
// lúc quyết toán (cọc, bồi thường, nợ, phần giữ lại, phần mất, phần hoàn, phần khách nộp thêm).
// Hệ thống không chuyển tiền: hoàn cọc và thu thêm làm ngoài hệ thống rồi quản lý xác nhận (việc 3.6c).
public class DepositSettlements
{
    public int DepositSettlementId { get; set; }
    public int ContractId { get; set; }
    public DateOnly MoveOutDate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal DamageCharges { get; set; }
    public decimal OutstandingDebt { get; set; }
    public decimal RetainedForDebt { get; set; }
    public decimal ForfeitedAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal AmountDue { get; set; }
    public bool EarlyWithoutNotice { get; set; }
    public int? NoticeDays { get; set; }
    public string? Note { get; set; }
    public int SettledById { get; set; }
    public DateTime SettledAtUtc { get; set; }
    public DateTime? RefundConfirmedAtUtc { get; set; }
    public int? RefundConfirmedById { get; set; }
    public DateTime? DueCollectedAtUtc { get; set; }
    public int? DueCollectedById { get; set; }

    public Contracts? Contract { get; set; }
    public Users? SettledBy { get; set; }
    public Users? RefundConfirmedBy { get; set; }
    public Users? DueCollectedBy { get; set; }
}
