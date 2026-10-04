namespace PRN232_BE.Models;

public class Service
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ICollection<RoomTypeServices> RoomTypeServices { get; set; } = new List<RoomTypeServices>();
}
