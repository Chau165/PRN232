using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class ViewingSchedulesApiTests
{
    // Luôn ở tương lai (2 ngày sau, 9h UTC) và tròn giây để so sánh chính xác.
    private static readonly DateTime FutureUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(9);

    [Theory]
    [InlineData("GET", "/api/viewing-schedules")]
    [InlineData("POST", "/api/viewing-schedules")]
    [InlineData("POST", "/api/viewing-schedules/1/approve")]
    [InlineData("POST", "/api/viewing-schedules/1/reject")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Customer_requests_a_viewing_and_the_owner_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var response = await client.PostAsJsonAsync("/api/viewing-schedules", new { roomId = 1, scheduledAt = FutureUtc });
        var duplicate = await client.PostAsJsonAsync("/api/viewing-schedules", new { roomId = 1, scheduledAt = (DateTime?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(FutureUtc, body.GetProperty("scheduledAt").GetDateTime());
        Assert.Equal(("A101", "Customer A"), (body.GetProperty("roomCode").GetString(), body.GetProperty("customerName").GetString()));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var scope = factory.Services.CreateScope();
        var notification = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.SingleAsync();
        Assert.Equal((7, "Viewing"), (notification.UserId, notification.Type));
    }

    [Theory]
    [InlineData(2, 1, HttpStatusCode.Conflict)]
    [InlineData(999, 1, HttpStatusCode.NotFound)]
    [InlineData(1, -1, HttpStatusCode.BadRequest)]
    public async Task Unavailable_unknown_or_past_requests_are_rejected(int roomId, int daysFromNow, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var response = await client.PostAsJsonAsync("/api/viewing-schedules",
            new { roomId, scheduledAt = DateTime.UtcNow.AddDays(daysFromNow) });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 1)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Viewing_list_follows_customer_owner_and_admin_scope(string email, int expectedCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        await CreateViewingAsync(client, scheduledAt: null);

        await LoginAsync(client, email);
        var viewings = await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules");

        Assert.Equal(expectedCount, viewings.GetArrayLength());
    }

    [Fact]
    public async Task Owner_must_pick_a_time_to_approve_an_owner_decides_request_and_customer_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, scheduledAt: null);
        await LoginAsync(client, "manager@example.test");

        var withoutTime = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new { });
        var approved = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new { scheduledAt = FutureUtc });
        var again = await client.PostAsync($"/api/viewing-schedules/{viewingId}/reject", null);

        Assert.Equal(HttpStatusCode.BadRequest, withoutTime.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Approved", FutureUtc), (body.GetProperty("status").GetString(), body.GetProperty("scheduledAt").GetDateTime()));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Viewing"));
    }

    [Theory]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_the_owner_or_an_admin_can_reject(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);

        await LoginAsync(client, email);
        var response = await client.PostAsync($"/api/viewing-schedules/{viewingId}/reject", null);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(("Rejected", FutureUtc), (body.GetProperty("status").GetString(), body.GetProperty("scheduledAt").GetDateTime()));
        }
    }

    private static async Task<int> CreateViewingAsync(HttpClient client, DateTime? scheduledAt)
    {
        using var response = await client.PostAsJsonAsync("/api/viewing-schedules", new { roomId = 1, scheduledAt });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("viewingId").GetInt32();
    }
}
