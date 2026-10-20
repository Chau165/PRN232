namespace PRN232.Domain.Entities;

public class PropertyAmenities
{
    public int PropertyId { get; set; }
    public int AmenityId { get; set; }

    public Property? Property { get; set; }
    public Amenities? Amenity { get; set; }
}
