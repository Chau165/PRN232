namespace PRN232.Domain.Entities;

public class RoomImages
{
    public int RoomImageId { get; set; }
    public int RoomId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public Rooms? Room { get; set; }
}
