namespace PRN232.Application.Policies;

public sealed record ServiceRateBillingDefault(string UnitCode, string BillingBasis);

public static class ServiceRateBillingDefaults
{
    private static readonly IReadOnlyDictionary<string, ServiceRateBillingDefault> DefaultsByServiceName =
        new Dictionary<string, ServiceRateBillingDefault>(StringComparer.OrdinalIgnoreCase)
        {
            // Keep English names compatible with historical catalogs; Vietnamese names match the current UI.
            ["Điện"] = new("kwh", "usage"),
            ["Nước"] = new("m3", "usage"),
            ["Rác"] = new("room_month", "fixed_room"),
            ["Gửi xe"] = new("vehicle_month", "vehicle"),
            ["Vệ sinh"] = new("room_month", "fixed_room"),
            ["An ninh"] = new("room_month", "fixed_room"),
            ["Người ở thêm"] = new("person_month", "additional_occupant"),
            ["Electricity"] = new("kwh", "usage"),
            ["Water"] = new("m3", "usage"),
            ["Internet"] = new("room_month", "fixed_room"),
            ["Garbage Collection"] = new("room_month", "fixed_room"),
            ["Parking"] = new("vehicle_month", "vehicle"),
            ["Cleaning"] = new("room_month", "fixed_room"),
            ["Security"] = new("room_month", "fixed_room"),
            ["Additional Occupant"] = new("person_month", "additional_occupant")
        };

    public static ServiceRateBillingDefault? ForServiceName(string? serviceName)
    {
        return string.IsNullOrWhiteSpace(serviceName)
            ? null
            : DefaultsByServiceName.GetValueOrDefault(serviceName.Trim());
    }
}
