namespace PRN232_BE.Models;

public class Users
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public Roles? Role { get; set; }
    public ICollection<Property> OwnedProperties { get; set; } = new List<Property>();
    public ICollection<Bookings> Bookings { get; set; } = new List<Bookings>();
    public ICollection<Contracts> ContractsAsCustomer { get; set; } = new List<Contracts>();
    public ICollection<Contracts> ContractsProcessed { get; set; } = new List<Contracts>();
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
    public ICollection<MaintenanceRequests> MaintenanceRequestsAssigned { get; set; } = new List<MaintenanceRequests>();
    public ICollection<MaintenanceLogs> MaintenanceLogsChanged { get; set; } = new List<MaintenanceLogs>();
    public ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();
    public ICollection<IdentityDocuments> IdentityDocumentsOwned { get; set; } = new List<IdentityDocuments>();
    public ICollection<IdentityDocuments> IdentityDocumentsReviewed { get; set; } = new List<IdentityDocuments>();
}
