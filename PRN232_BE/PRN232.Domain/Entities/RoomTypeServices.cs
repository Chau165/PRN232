namespace PRN232.Domain.Entities;

public class RoomTypeServices
{
    public int RoomTypeId { get; set; }
    public int ServiceId { get; set; }

    public RoomTypes? RoomType { get; set; }
    public Service? Service { get; set; }
}
