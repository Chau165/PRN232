namespace PRN232.Domain.Entities;
public class ContractIdentitySnapshot
{
    public int ContractIdentitySnapshotId { get; set; }
    public int ContractId { get; set; }
    public Contracts Contract { get; set; } = null!;
    public int SourceApplicationId { get; set; }
    public int SourcePersonId { get; set; }
    public int SourceRevision { get; set; }
    public Guid SourceApplicationVersion { get; set; }
    public int Position { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string FrontObjectPath { get; set; } = string.Empty;
    public string BackObjectPath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
public class BookingIdentityCleanupTask
{
    public int BookingIdentityCleanupTaskId { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public int? ApplicationId { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}
