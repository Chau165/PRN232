namespace PRN232_BE.Models;

public class Notifications
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public Users? User { get; set; }
}
