namespace PRN232.Domain.Entities;

public class Rooms
{
    public int RoomId { get; set; }
    public int PropertyId { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public decimal AreaSqm { get; set; }
    public string Status { get; set; } = string.Empty;

    public Property? Property { get; set; }
    public RoomTypes? RoomType { get; set; }
    public ICollection<RoomImages> RoomImages { get; set; } = new List<RoomImages>();
    public ICollection<RoomAmenities> RoomAmenities { get; set; } = new List<RoomAmenities>();
    public ICollection<Equipments> Equipments { get; set; } = new List<Equipments>();
    public ICollection<Bookings> Bookings { get; set; } = new List<Bookings>();
    public ICollection<Contracts> Contracts { get; set; } = new List<Contracts>();
}
