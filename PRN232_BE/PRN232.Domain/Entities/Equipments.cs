namespace PRN232.Domain.Entities;

public class Equipments
{
    public int EquipmentId { get; set; }
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly InstallDate { get; set; }
    public DateOnly WarrantyExpiry { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Rooms? Room { get; set; }
    public ICollection<MaintenanceRequests> MaintenanceRequests { get; set; } = new List<MaintenanceRequests>();
}
