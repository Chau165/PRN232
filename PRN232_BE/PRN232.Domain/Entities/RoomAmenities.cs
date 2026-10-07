namespace PRN232.Domain.Entities;

public class RoomAmenities
{
    public int RoomId { get; set; }
    public int AmenityId { get; set; }

    public Rooms? Room { get; set; }
    public Amenities? Amenity { get; set; }
}
