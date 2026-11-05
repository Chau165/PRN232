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
    public async Task Customer_request_notifies_each_active_admin_after_the_viewing_is_committed()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var publisher = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>();
        var observedCommittedState = false;
        publisher.OnCreatedAsync = async notifications =>
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var committedViewing = await context.ViewingSchedules.SingleOrDefaultAsync();
            var committedNotifications = await context.Notifications.CountAsync();
            observedCommittedState = committedViewing is { Status: "Pending" } && committedNotifications == notifications.Count;
        };

        var response = await client.PostAsJsonAsync("/api/viewing-schedules", new
        {
            roomId = 1,
            scheduledAt = FutureUtc
        });
        var duplicate = await client.PostAsJsonAsync("/api/viewing-schedules", new
        {
            roomId = 1,
            scheduledAt = (DateTime?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(FutureUtc, body.GetProperty("scheduledAt").GetDateTime());
        Assert.Equal(("A101", "Customer A"), (body.GetProperty("roomCode").GetString(), body.GetProperty("customerName").GetString()));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var pendingPushes = publisher.Created;
        Assert.Equal(new[] { 9, 11 }, pendingPushes.Select(notification => notification.UserId).Order().ToArray());
        Assert.All(pendingPushes, notification =>
        {
            Assert.Equal("Viewing", notification.Type);
            Assert.Equal(body.GetProperty("viewingId").GetInt32(), notification.RelatedEntityId);
        });
        Assert.True(observedCommittedState);
        using var scope = factory.Services.CreateScope();
        var notifications = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.ToListAsync();
        Assert.Equal(new[] { 9, 11 }, notifications.Select(notification => notification.UserId).Order().ToArray());
        Assert.DoesNotContain(notifications, notification => notification.UserId == 7 || notification.UserId == 8);
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
            new
            {
                roomId,
                scheduledAt = DateTime.UtcNow.AddDays(daysFromNow)
            });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 0)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Viewing_list_follows_customer_assigned_manager_and_admin_scope(string email, int expectedCount)
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
    public async Task Admin_assigns_active_manager_and_notifies_manager_and_customer()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var customerToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var viewingId = await CreateViewingAsync(client, scheduledAt: null);
        await LoginAsync(client, "manager@example.test");

        var managerDecision = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc,
            assignedTo = 7
        });
        await LoginAsync(client, "admin@example.test");
        var withoutAssignment = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc
        });
        var publisher = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>();
        var decisionWasCommittedBeforePublish = false;
        publisher.OnCreatedAsync = async notifications =>
        {
            using var verifyScope = factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var committed = await verifyContext.ViewingSchedules.SingleAsync(viewing => viewing.ViewingId == viewingId);
            var relatedNotificationCount = await verifyContext.Notifications.CountAsync(notification => notification.RelatedEntityId == viewingId);
            decisionWasCommittedBeforePublish = committed.Status == "Approved" && committed.AssignedTo == 7 &&
                committed.RespondedBy == 9 && committed.RespondedAt.HasValue && relatedNotificationCount == notifications.Count + 2;
        };
        var approved = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc,
            assignedTo = 7,
            managerNote = "Please arrive at the front gate."
        });
        var again = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject", new { reason = "Already handled." });

        Assert.Equal(HttpStatusCode.Forbidden, managerDecision.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.True(decisionWasCommittedBeforePublish);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Approved", FutureUtc), (body.GetProperty("status").GetString(), body.GetProperty("scheduledAt").GetDateTime()));
        Assert.Equal(7, body.GetProperty("assignedTo").GetInt32());
        Assert.Equal(("Manager A", "0911000000"),
            (body.GetProperty("assignedManagerName").GetString(), body.GetProperty("assignedManagerPhone").GetString()));
        Assert.Equal("Please arrive at the front gate.", body.GetProperty("managerNote").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.Type == "Viewing"));
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 7 && notification.Type == "Viewing"));
        var pushes = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>().Created;
        Assert.DoesNotContain(pushes, notification => notification.UserId == 8);
        var responsePush = Assert.Single(pushes, notification => notification.UserId == 10 && notification.RelatedEntityId == viewingId);
        Assert.Contains("chấp thuận", responsePush.Title);
        Assert.Single(pushes, notification => notification.UserId == 7 && notification.RelatedEntityId == viewingId);

        await LoginAsync(client, "manager@example.test");
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules")).GetArrayLength());
        await LoginAsync(client, "manager-b@example.test");
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/viewing-schedules/{viewingId}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", customerToken);
        var customerNotifications = await client.GetFromJsonAsync<JsonElement>("/api/notifications");
        var viewingNotification = Assert.Single(customerNotifications.EnumerateArray(),
            notification => notification.GetProperty("relatedEntityId").GetInt32() == viewingId);
        Assert.Contains("Please arrive at the front gate.", viewingNotification.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("manager@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_admin_can_reject_and_rejection_reason_is_saved(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);

        await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject", new { reason = "The room is temporarily unavailable." });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(("Rejected", FutureUtc), (body.GetProperty("status").GetString(), body.GetProperty("scheduledAt").GetDateTime()));
            Assert.Equal("The room is temporarily unavailable.", body.GetProperty("managerNote").GetString());
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10 && notification.RelatedEntityId == viewingId));
            Assert.Equal(0, await context.Notifications.CountAsync(notification => notification.UserId == 7 && notification.RelatedEntityId == viewingId));
        }
    }

    [Fact]
    public async Task Admin_cannot_assign_inactive_manager_or_oversized_note()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);
        await LoginAsync(client, "admin@example.test");

        var inactiveManager = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc,
            assignedTo = 14
        });
        var oversizedNote = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc,
            assignedTo = 7,
            managerNote = new string('x', 501)
        });

        Assert.Equal(HttpStatusCode.BadRequest, inactiveManager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedNote.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal("Pending", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ViewingSchedules
            .SingleAsync(viewing => viewing.ViewingId == viewingId)).Status);
    }

    [Fact]
    public async Task Historical_unassigned_pending_viewings_remain_visible_to_admin_not_managers()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, null);

        await LoginAsync(client, "admin@example.test");
        var adminList = await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules");
        var historical = Assert.Single(adminList.EnumerateArray(), item => item.GetProperty("viewingId").GetInt32() == viewingId);
        Assert.True(historical.GetProperty("assignedTo").ValueKind == JsonValueKind.Null);
        Assert.True(historical.GetProperty("managerNote").ValueKind == JsonValueKind.Null);
        Assert.True(historical.GetProperty("respondedAt").ValueKind == JsonValueKind.Null);
        await LoginAsync(client, "manager@example.test");
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules")).EnumerateArray());
    }

    [Fact]
    public async Task Reject_requires_a_reason_and_a_rejected_request_returns_conflict_on_replay()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);
        await LoginAsync(client, "admin@example.test");

        var missingReason = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject", new { reason = " " });
        var rejected = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject", new { reason = "No available staff." });
        var replay = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject", new { reason = "Another reason." });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    private static async Task<int> CreateViewingAsync(HttpClient client, DateTime? scheduledAt)
    {
        using var response = await client.PostAsJsonAsync("/api/viewing-schedules", new
        {
            roomId = 1,
            scheduledAt
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("viewingId").GetInt32();
    }
}
