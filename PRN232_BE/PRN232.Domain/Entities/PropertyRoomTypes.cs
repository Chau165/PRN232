namespace PRN232.Domain.Entities;

public class PropertyRoomTypes
{
    public int PropertyId { get; set; }
    public int RoomTypeId { get; set; }
    public decimal MonthlyRent { get; set; }
    public bool IsActive { get; set; } = true;

    public Property? Property { get; set; }
    public RoomTypes? RoomType { get; set; }
    public ICollection<Rooms> Rooms { get; set; } = new List<Rooms>();
}
