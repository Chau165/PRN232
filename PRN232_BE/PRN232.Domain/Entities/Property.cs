namespace PRN232.Domain.Entities;

public class Property
{
    public int PropertyId
    {
        get; set;
    }
    public int OwnerId
    {
        get; set;
    }
    public string PropertyName { get; set; } = string.Empty;
    public string? PrimaryImageUrl
    {
        get; set;
    }
    public string? Description
    {
        get; set;
    }
    public string AddressText { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    // Mã đơn vị hành chính sau sáp nhập (danh mục NSO). Rỗng = khu trọ cũ chưa được gán mã.
    public string ProvinceCode { get; set; } = string.Empty;
    public string WardCode { get; set; } = string.Empty;
    public decimal Latitude
    {
        get; set;
    }
    public decimal Longitude
    {
        get; set;
    }

    public Users? Owner
    {
        get; set;
    }
    public ICollection<Rooms> Rooms { get; set; } = new List<Rooms>();
    public ICollection<PropertyRoomTypes> PropertyRoomTypes { get; set; } = new List<PropertyRoomTypes>();
    public ICollection<PropertyAmenities> PropertyAmenities { get; set; } = new List<PropertyAmenities>();
    public ICollection<PropertyServiceRates> PropertyServiceRates { get; set; } = new List<PropertyServiceRates>();
    public ICollection<PropertyManagerAssignment> ManagerAssignments { get; set; } = new List<PropertyManagerAssignment>();
}
