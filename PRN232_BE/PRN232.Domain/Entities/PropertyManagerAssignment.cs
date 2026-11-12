namespace PRN232.Domain.Entities;

public sealed class PropertyManagerAssignment
{
    public int PropertyId { get; set; }
    public int ManagerId { get; set; }

    public Property? Property { get; set; }
    public Users? Manager { get; set; }
}
