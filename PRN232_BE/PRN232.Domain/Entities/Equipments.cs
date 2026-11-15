namespace PRN232.Domain.Entities;

public class Equipments
{
    public int EquipmentId
    {
        get; set;
    }
    public int RoomId
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public DateOnly InstallDate
    {
        get; set;
    }
    public DateOnly WarrantyExpiry
    {
        get; set;
    }
    public string Supplier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    // Giá trị bồi thường MỖI cái, ghi vào biên bản bàn giao. Là mức trần khi quyết toán cọc: khách chỉ
    // bồi thường hư hỏng do mình gây ra, không bồi thường hao mòn tự nhiên (BLDS 2015, Điều 479).
    public decimal CompensationValue
    {
        get; set;
    }

    public Rooms? Room
    {
        get; set;
    }
    public ICollection<MaintenanceRequests> MaintenanceRequests { get; set; } = new List<MaintenanceRequests>();
}
