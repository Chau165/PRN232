namespace PRN232_BE.Models;

public class UtilityReadings
{
    public int ReadingId { get; set; }
    public int ContractId { get; set; }
    public DateOnly ReadingMonth { get; set; }
    public decimal ElectricStart { get; set; }
    public decimal ElectricEnd { get; set; }
    public decimal WaterStart { get; set; }
    public decimal WaterEnd { get; set; }

    public Contracts? Contract { get; set; }
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
}
