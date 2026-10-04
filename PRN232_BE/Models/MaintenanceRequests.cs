namespace PRN232_BE.Models;

public class MaintenanceRequests
{
    public int RequestId { get; set; }
    public int ContractId { get; set; }
    public int? EquipmentId { get; set; }
    public int? AssignedTo { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Contracts? Contract { get; set; }
    public Equipments? Equipment { get; set; }
    public Users? AssignedToUser { get; set; }
    public ICollection<MaintenanceLogs> MaintenanceLogs { get; set; } = new List<MaintenanceLogs>();
}
