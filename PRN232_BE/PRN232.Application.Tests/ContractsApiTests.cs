using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class ContractsApiTests
{
    private static readonly DateOnly VietnamToday = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    [Theory]
    [InlineData("GET", "/api/contracts")]
    [InlineData("POST", "/api/contracts")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Owner_turns_a_paid_booking_into_a_contract_that_the_tenant_sees_as_a_current_rental()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");

        var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var again = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1));
        var cancel = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Active", "A101", "Manager A"), (body.GetProperty("status").GetString(),
            body.GetProperty("roomCode").GetString(), body.GetProperty("processedByStaffName").GetString()));
        Assert.Equal((3_000_000m, 3_000_000m), (body.GetProperty("monthlyRent").GetDecimal(), body.GetProperty("depositAmount").GetDecimal()));
        Assert.Equal(VietnamToday.ToString("yyyy-MM-dd"), body.GetProperty("startDate").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Occupied", (await context.Rooms.SingleAsync(room => room.RoomId == 1)).Status);
            Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Contract"));
        }

        await LoginAsync(client, "customer@example.test");
        var booking = await client.GetFromJsonAsync<JsonElement>($"/api/bookings/{bookingId}");
        var rentals = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals");
        Assert.Equal(body.GetProperty("contractId").GetInt32(), booking.GetProperty("contractId").GetInt32());
        Assert.Equal(1, rentals.GetArrayLength());
    }

    [Theory]
    [InlineData("admin@example.test", HttpStatusCode.Created)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_the_owner_or_an_admin_can_create_a_contract(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);

        await LoginAsync(client, email);
        var response = await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddMonths(6));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Unpaid_or_unknown_bookings_are_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var unpaid = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        var unpaidId = (await unpaid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        await LoginAsync(client, "manager@example.test");

        var fromUnpaid = await PostContractAsync(client, unpaidId, VietnamToday, VietnamToday.AddYears(1));
        var unknown = await PostContractAsync(client, 999, VietnamToday, VietnamToday.AddYears(1));

        Assert.Equal(HttpStatusCode.Conflict, fromUnpaid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData(-1, 365)]
    [InlineData(0, 0)]
    public async Task Start_date_in_the_past_or_end_not_after_start_is_rejected(int startOffsetDays, int termDays)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "manager@example.test");
        var start = VietnamToday.AddDays(startOffsetDays);

        var response = await PostContractAsync(client, bookingId, start, start.AddDays(termDays));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 1)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Contract_list_follows_customer_owner_and_admin_scope(string email, int expectedCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var bookingId = await CreatePaidBookingAsync(client);
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.Created, (await PostContractAsync(client, bookingId, VietnamToday, VietnamToday.AddYears(1))).StatusCode);

        await LoginAsync(client, email);
        var contracts = await client.GetFromJsonAsync<JsonElement>("/api/contracts");

        Assert.Equal(expectedCount, contracts.GetArrayLength());
    }

    private static Task<HttpResponseMessage> PostContractAsync(HttpClient client, int bookingId, DateOnly startDate, DateOnly endDate) =>
        client.PostAsJsonAsync("/api/contracts", new { bookingId, startDate, endDate });

    private static async Task<int> CreatePaidBookingAsync(HttpClient client)
    {
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        var paid = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return bookingId;
    }
}
