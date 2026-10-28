namespace PRN232.Domain.Entities;

public class ViewingSchedules
{
    public int ViewingId { get; set; }
    public int RoomId { get; set; }
    public int CustomerId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;

    public Rooms? Room { get; set; }
    public Users? Customer { get; set; }
}
