namespace PRN232.Domain.Entities;

public class Amenities
{
    public int AmenityId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<RoomAmenities> RoomAmenities { get; set; } = new List<RoomAmenities>();
}
