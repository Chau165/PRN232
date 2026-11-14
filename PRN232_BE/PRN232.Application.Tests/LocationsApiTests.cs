using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 1.4: the administrative catalog is public reference data, so guests can filter rooms by it.
public sealed class LocationsApiTests
{
    [Fact]
    public async Task Guests_can_read_provinces_and_wards()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();

        using var provinces = await client.GetAsync("/api/locations/provinces");
        Assert.Equal(HttpStatusCode.OK, provinces.StatusCode);
        Assert.Equal(34, (await provinces.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        using var wards = await client.GetAsync("/api/locations/wards?provinceCode=79");
        Assert.Equal(HttpStatusCode.OK, wards.StatusCode);
        Assert.Contains((await wards.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            ward => ward.GetProperty("wardCode").GetString() == "26743");
    }
}
