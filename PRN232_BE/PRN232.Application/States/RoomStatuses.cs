namespace PRN232.Application.States;

public static class RoomStatuses
{
    public const string Available = "Available";
    public const string Occupied = "Occupied";
    public const string Maintenance = "Maintenance";
    public const string Reserved = "Reserved";

    public static readonly string[] All = { Available, Occupied, Maintenance, Reserved };
}
