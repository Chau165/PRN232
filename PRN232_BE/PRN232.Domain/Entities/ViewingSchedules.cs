namespace PRN232.Domain.Entities;

public class ViewingSchedules
{
    public int ViewingId
    {
        get; set;
    }
    public int PropertyId { get; set; }
    public int RoomTypeId { get; set; }
    public int? RoomId
    {
        get; set;
    }
    public int CustomerId
    {
        get; set;
    }
    public DateTime? ScheduledAt
    {
        get; set;
    }
    public string Status { get; set; } = string.Empty;

    public int? AssignedTo { get; set; }
    public string? ManagerNote { get; set; }
    public int? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Rooms? Room
    {
        get; set;
    }
    public Property? Property { get; set; }
    public RoomTypes? RoomType { get; set; }
    public Users? Customer
    {
        get; set;
    }
    public Users? AssignedManager { get; set; }
    public Users? RespondedByUser { get; set; }
}
