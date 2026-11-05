namespace PRN232.Application.States;

public static class EquipmentStatuses
{
    public const string Operational = "Operational";

    public static bool IsValid(string status)
    {
        return status == Operational;
    }
}
