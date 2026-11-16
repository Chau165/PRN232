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
    public async Task Customer_request_notifies_the_active_manager_of_the_property_after_commit()
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
        Assert.Equal(new[] { 7 }, pendingPushes.Select(notification => notification.UserId).Order().ToArray());
        Assert.All(pendingPushes, notification =>
        {
            Assert.Equal("Viewing", notification.Type);
            Assert.Equal(body.GetProperty("viewingId").GetInt32(), notification.RelatedEntityId);
        });
        Assert.True(observedCommittedState);
        using var scope = factory.Services.CreateScope();
        var notifications = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.ToListAsync();
        Assert.Equal(new[] { 7 }, notifications.Select(notification => notification.UserId).Order().ToArray());
        Assert.DoesNotContain(notifications, notification => notification.UserId == 9 || notification.UserId == 11 || notification.UserId == 8);
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
    [InlineData("manager@example.test", 1)]
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
    public async Task Assigned_manager_approves_request_and_customer_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, scheduledAt: null);

        await LoginAsync(client, "admin@example.test");
        var adminDecision = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc
        });
        Assert.Equal(HttpStatusCode.Forbidden, adminDecision.StatusCode);

        await LoginAsync(client, "manager-b@example.test");
        var wrongManager = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc
        });
        Assert.Equal(HttpStatusCode.Forbidden, wrongManager.StatusCode);

        await LoginAsync(client, "manager@example.test");
        var publisher = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>();
        var committedBeforePublish = false;
        publisher.OnCreatedAsync = async notifications =>
        {
            using var checkScope = factory.Services.CreateScope();
            var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await db.ViewingSchedules.SingleAsync(v => v.ViewingId == viewingId);
            committedBeforePublish = persisted.Status == "Approved" && persisted.AssignedTo == 7 &&
                persisted.RespondedBy == 7 && persisted.RespondedAt.HasValue &&
                await db.Notifications.CountAsync(n => n.RelatedEntityId == viewingId) == 2 &&
                notifications.Count == 1;
        };

        var approved = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve", new
        {
            scheduledAt = FutureUtc,
            managerNote = "Please arrive at the front gate."
        });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.True(committedBeforePublish);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal(FutureUtc, body.GetProperty("scheduledAt").GetDateTime());
        Assert.Equal(7, body.GetProperty("assignedTo").GetInt32());
        Assert.Equal("Manager A", body.GetProperty("assignedManagerName").GetString());
        Assert.Equal("Please arrive at the front gate.", body.GetProperty("managerNote").GetString());

        var replay = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/reject",
            new { reason = "Too late" });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        using var scope = factory.Services.CreateScope();
        var notifications = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .Where(n => n.RelatedEntityId == viewingId).ToListAsync();
        Assert.Equal(1, notifications.Count(n => n.UserId == 7));
        Assert.Equal(1, notifications.Count(n => n.UserId == 10));
        Assert.DoesNotContain(notifications, n => n.UserId == 9 || n.UserId == 11 || n.UserId == 8);
    }

    [Theory]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("admin@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("customer@example.test", HttpStatusCode.Forbidden)]
    public async Task Only_assigned_property_manager_can_reject_and_rejection_reason_is_saved(string email, HttpStatusCode expected)
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
    public async Task Manager_cannot_approve_with_oversized_note_or_after_assignment_is_removed()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);
        await LoginAsync(client, "manager@example.test");

        var oversizedNote = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve",
            new { scheduledAt = FutureUtc, managerNote = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, oversizedNote.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.PropertyManagerAssignments.SingleAsync(a => a.PropertyId == 1 && a.ManagerId == 7);
            db.PropertyManagerAssignments.Remove(assignment);
            await db.SaveChangesAsync();
        }

        var revoked = await client.PostAsJsonAsync($"/api/viewing-schedules/{viewingId}/approve",
            new { scheduledAt = FutureUtc });
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.Equal("Pending", (await verify.ServiceProvider.GetRequiredService<AppDbContext>().ViewingSchedules
            .SingleAsync(v => v.ViewingId == viewingId)).Status);
    }

    [Fact]
    public async Task Unassigned_pending_viewings_are_visible_to_assigned_property_manager()
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
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/viewing-schedules")).GetArrayLength());
    }

    [Fact]
    public async Task Reject_requires_a_reason_and_a_rejected_request_returns_conflict_on_replay()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var viewingId = await CreateViewingAsync(client, FutureUtc);
        await LoginAsync(client, "manager@example.test");

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
