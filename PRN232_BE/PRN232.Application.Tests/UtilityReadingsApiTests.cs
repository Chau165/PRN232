using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
using static PRN232.Application.Tests.ContractsApiTests;

namespace PRN232.Application.Tests;

public class UtilityReadingsApiTests
{
    private static readonly DateOnly VietnamToday = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static readonly DateOnly ThisMonth = new(VietnamToday.Year, VietnamToday.Month, 1);

    [Theory]
    [InlineData("GET", "/api/utility-readings")]
    [InlineData("POST", "/api/utility-readings")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Owner_records_the_first_reading_once_per_month_and_the_tenant_can_see_it()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        await LoginAsync(client, "manager@example.test");

        var created = await PostReadingAsync(client, contractId, ThisMonth, 150m, 14m, 100m, 10m);
        var again = await PostReadingAsync(client, contractId, ThisMonth, 160m, 15m, null, null);
        var nextMonth = await PostReadingAsync(client, contractId, ThisMonth.AddMonths(1), 170m, 16m, null, null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((50m, 4m), (body.GetProperty("electricUsage").GetDecimal(), body.GetProperty("waterUsage").GetDecimal()));
        Assert.Equal(ThisMonth.ToString("yyyy-MM-dd"), body.GetProperty("readingMonth").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nextMonth.StatusCode);

        await LoginAsync(client, "customer@example.test");
        var mine = await client.GetFromJsonAsync<JsonElement>($"/api/utility-readings?contractId={contractId}");
        await LoginAsync(client, "other@example.test");
        var someoneElse = await client.GetFromJsonAsync<JsonElement>("/api/utility-readings");
        Assert.Equal((1, 0), (mine.GetArrayLength(), someoneElse.GetArrayLength()));
    }

    [Theory]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.Created)]
    public async Task Only_the_owner_or_an_admin_can_record_readings(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);

        await LoginAsync(client, email);
        var response = await PostReadingAsync(client, contractId, ThisMonth, 150m, 14m, 100m, 10m);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Lower_end_values_or_missing_first_start_values_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var contractId = await CreateContractAsync(client);
        await LoginAsync(client, "manager@example.test");

        var lower = await PostReadingAsync(client, contractId, ThisMonth, 90m, 14m, 100m, 10m);
        var noStart = await PostReadingAsync(client, contractId, ThisMonth, 150m, 14m, null, null);
        var unknown = await PostReadingAsync(client, 999, ThisMonth, 150m, 14m, 100m, 10m);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.NotFound),
            (lower.StatusCode, noStart.StatusCode, unknown.StatusCode));
    }

    private static Task<HttpResponseMessage> PostReadingAsync(HttpClient client, int contractId, DateOnly readingMonth,
        decimal electricEnd, decimal waterEnd, decimal? electricStart, decimal? waterStart) =>
        client.PostAsJsonAsync("/api/utility-readings",
            new { contractId, readingMonth, electricEnd, waterEnd, electricStart, waterStart });

    // Hợp đồng bắt đầu hôm nay nên tháng hiện tại là tháng duy nhất ghi được chỉ số trong test.
    private static async Task<int> CreateContractAsync(HttpClient client)
    {
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "admin@example.test");
        var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractId").GetInt32();
    }
}
