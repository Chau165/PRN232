namespace PRN232.Domain.Entities;
public class BookingIdentityRetentionAudit
{
    public int BookingIdentityRetentionAuditId { get; set; }
    public int? ApplicationId { get; set; }
    public int? TaskId { get; set; }
    public string Event { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
