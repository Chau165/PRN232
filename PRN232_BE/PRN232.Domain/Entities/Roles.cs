namespace PRN232.Domain.Entities;

public class Roles
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<Users> Users { get; set; } = new List<Users>();
}
