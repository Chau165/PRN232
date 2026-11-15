namespace PRN232.Domain.Entities;

// Rebuild 3.5 (PLAN.md D3 bước 4 và 6): biên bản bàn giao thiết bị lúc nhận phòng (MoveIn) và trả phòng
// (MoveOut). Mỗi hợp đồng có tối đa một biên bản mỗi loại. Là căn cứ tính bồi thường khi quyết toán cọc.
public class HandoverRecords
{
    public int HandoverRecordId { get; set; }
    public int ContractId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateOnly HandoverDate { get; set; }
    public string? Note { get; set; }
    public int RecordedById { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    public Contracts? Contract { get; set; }
    public Users? RecordedBy { get; set; }
    public ICollection<HandoverItems> Items { get; set; } = new List<HandoverItems>();
}

// Tên, số lượng và giá trị bồi thường được CHỤP LẠI lúc lập biên bản: sửa thiết bị sau này không đổi
// biên bản đã thống nhất. EquipmentId thành NULL nếu thiết bị bị xóa, biên bản vẫn còn nguyên.
public class HandoverItems
{
    public int HandoverItemId { get; set; }
    public int HandoverRecordId { get; set; }
    public int? EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal CompensationValue { get; set; }
    public string Condition { get; set; } = string.Empty;
    public decimal ChargeAmount { get; set; }
    public string? Note { get; set; }

    public HandoverRecords? HandoverRecord { get; set; }
    public Equipments? Equipment { get; set; }
}
