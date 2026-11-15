namespace PRN232.Domain.Entities;

// Rebuild 4.1 (PLAN.md D4): người ở cùng phòng với người đứng tên hợp đồng (người đứng tên = Contracts.CustomerId,
// không lưu ở đây nhưng vẫn tính là 1 người). Chuyển đi thì ghi MoveOutDate, không xóa (giữ lịch sử tạm trú và
// để tính dịch vụ theo đầu người). IdentityNumber (CCCD) được MÃ HÓA khi ghi (CccdProtection), API chỉ trả số đã
// che; xem đủ số phải gọi API riêng và được ghi nhật ký.
public class ContractOccupants
{
    public int ContractOccupantId { get; set; }
    public int ContractId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public DateOnly MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }
    public string? Note { get; set; }
    public int AddedById { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Contracts? Contract { get; set; }
    public Users? AddedBy { get; set; }
}
