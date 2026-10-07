namespace PRN232.Domain.Entities;

public class Contracts
{
    public int ContractId { get; set; }
    public int RoomId { get; set; }
    public int CustomerId { get; set; }
    public int? BookingId { get; set; }
    public int? ProcessedByStaffId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    public Rooms? Room { get; set; }
    public Users? Customer { get; set; }
    public Bookings? Booking { get; set; }
    public Users? ProcessedByStaff { get; set; }
    public ICollection<UtilityReadings> UtilityReadings { get; set; } = new List<UtilityReadings>();
    public ICollection<Invoices> Invoices { get; set; } = new List<Invoices>();
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
    public ICollection<MaintenanceRequests> MaintenanceRequests { get; set; } = new List<MaintenanceRequests>();
}
