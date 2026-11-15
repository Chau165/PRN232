namespace PRN232.Application.Policies;

public sealed record ServiceRateBillingDefault(string UnitCode, string BillingBasis);

public static class ServiceRateBillingDefaults
{
    private static readonly IReadOnlyDictionary<string, ServiceRateBillingDefault> DefaultsByServiceName =
        new Dictionary<string, ServiceRateBillingDefault>(StringComparer.OrdinalIgnoreCase)
        {
            ["Electricity"] = new("kwh", "usage"),
            ["Water"] = new("m3", "usage"),
            ["Internet"] = new("room_month", "fixed_room"),
            ["Garbage Collection"] = new("room_month", "fixed_room"),
            ["Parking"] = new("vehicle_month", "vehicle"),
            ["Cleaning"] = new("room_month", "fixed_room"),
            ["Security"] = new("room_month", "fixed_room"),
            ["Additional Occupant"] = new("person_month", "occupant")
        };

    public static ServiceRateBillingDefault? ForServiceName(string? serviceName)
    {
        return string.IsNullOrWhiteSpace(serviceName)
            ? null
            : DefaultsByServiceName.GetValueOrDefault(serviceName.Trim());
    }
}
