namespace PRN232_BE.Models;

public class RoomTypes
{
    public int RoomTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ICollection<Rooms> Rooms { get; set; } = new List<Rooms>();
    public ICollection<RoomTypeServices> RoomTypeServices { get; set; } = new List<RoomTypeServices>();
}
