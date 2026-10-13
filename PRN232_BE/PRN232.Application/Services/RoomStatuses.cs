namespace PRN232.Application.Services;

public static class RoomStatuses
{
    public const string Available = "Available";
    public const string Occupied = "Occupied";
    public const string Maintenance = "Maintenance";

    public static readonly string[] All = { Available, Occupied, Maintenance };
}
