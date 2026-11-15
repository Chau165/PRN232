namespace PRN232.Domain.Entities;

// Rebuild 4.2 (PLAN.md D4): người đứng tên chuyển đi, người ở cùng ở tiếp. Nhóm đã chốt:
// - Cọc CHUYỂN NGUYÊN sang hợp đồng mới (hai người tự thanh toán phần cọc với nhau ngoài hệ thống).
// - Hiệu lực từ NGÀY 1 của một tháng (điện nước, hóa đơn chốt theo tháng; không chia tiền giữa hai người).
// - Người nhận phải có tài khoản khách; ba bên xác nhận: người cũ, người mới, rồi quản lý bấm hoàn tất.
// - Biên bản trả phòng của hợp đồng cũ có thiết bị hư hỏng/mất thì không chuyển được (đi đường trả phòng thường).
public class ContractTransfers
{
    public int ContractTransferId { get; set; }
    public int FromContractId { get; set; }
    public int NewHolderId { get; set; }
    // Người ở cùng (của hợp đồng cũ) trở thành người đứng tên; null nếu người nhận chưa có trong danh sách.
    public int? BecomingHolderOccupantId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RequestedById { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? OldHolderConfirmedAtUtc { get; set; }
    public DateTime? NewHolderConfirmedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedById { get; set; }
    public int? NewContractId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledById { get; set; }
    public string? CancelReason { get; set; }

    public Contracts? FromContract { get; set; }
    public Contracts? NewContract { get; set; }
    public Users? NewHolder { get; set; }
    public Users? RequestedBy { get; set; }
}
