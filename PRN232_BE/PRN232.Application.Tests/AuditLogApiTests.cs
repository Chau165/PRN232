using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

// Rebuild 5.1 (PLAN.md D7, layer 1): every POST/PUT/PATCH/DELETE is recorded with who, what, which object and
// the outcome — including failures — without storing request bodies; only Admin can read the log.
public sealed class AuditLogApiTests
{
    [Fact]
    public async Task Writes_are_recorded_with_user_object_and_outcome_but_reads_are_not()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var created = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        await client.GetAsync($"/api/bookings/{bookingId}");
        var invalid = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1, occupantCount = 11 });
        await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var scope = factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .Where(log => log.EntityType == "Bookings").OrderBy(log => log.AuditLogId).ToListAsync();
        Assert.Equal(
            [("Bookings.Create", bookingId.ToString(), 201, true), ("Bookings.Create", null, 400, false),
             ("Bookings.Cancel", bookingId.ToString(), 200, true)],
            logs.Select(log => (log.Action, log.EntityId, log.StatusCode, log.Succeeded)));
        Assert.All(logs, log => Assert.Equal((10, "Customer", "POST"), (log.UserId!.Value, log.Role, log.HttpMethod)));
        Assert.Equal($"/api/bookings/{bookingId}/cancel", logs[2].Path);
    }

    [Fact]
    public async Task Request_bodies_such_as_passwords_are_never_stored()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new { email = "customer@example.test", password = "wrong-secret-123" });
        await LoginAsync(client, "customer@example.test");

        using var scope = factory.Services.CreateScope();
        var logins = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .Where(log => log.Action == "Auth.Login").ToListAsync();
        Assert.Equal([false, true], logins.OrderBy(log => log.AuditLogId).Select(log => log.Succeeded));
        Assert.All(logins, log => Assert.DoesNotContain("secret", JsonSerializer.Serialize(log), StringComparison.OrdinalIgnoreCase));
        Assert.All(logins, log => Assert.DoesNotContain(Password, JsonSerializer.Serialize(log), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Only_admin_searches_the_log_with_filters()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        var byCustomer = await client.GetAsync("/api/audit-logs");

        await LoginAsync(client, "admin@example.test");
        var page = await client.GetFromJsonAsync<JsonElement>("/api/audit-logs?entityType=Bookings&userId=10&page=1&pageSize=10");
        var tooLarge = await client.GetAsync("/api/audit-logs?pageSize=1000");

        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(("Bookings.Create", "Customer A"), (item.GetProperty("action").GetString(), item.GetProperty("userName").GetString()));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [Fact]
    public async Task A_failing_audit_store_never_breaks_the_users_action()
    {
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditLogWriter>();
            services.AddSingleton<IAuditLogWriter, BrokenWriter>();
        }));
        using var client = app.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var created = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    // Rebuild 5.2 (layer 2): important business actions also keep the values before/after, on the same log row.
    [Fact]
    public async Task Booking_actions_keep_values_before_and_after_but_failures_keep_none()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var created = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1, occupantCount = 1 });
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
        await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);
        var again = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var scope = factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .Where(log => log.EntityType == "Bookings").OrderBy(log => log.AuditLogId).ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.Null(logs[0].OldValues);
        var createdValues = JsonDocument.Parse(logs[0].NewValues!).RootElement;
        Assert.Equal(("A101", "Pending", 1), (createdValues.GetProperty("roomCode").GetString(),
            createdValues.GetProperty("status").GetString(), createdValues.GetProperty("occupantCount").GetInt32()));
        Assert.Equal("Pending", JsonDocument.Parse(logs[1].OldValues!).RootElement.GetProperty("status").GetString());
        var cancelled = JsonDocument.Parse(logs[1].NewValues!).RootElement;
        Assert.Equal(("Cancelled", "CustomerCancelled"),
            (cancelled.GetProperty("status").GetString(), cancelled.GetProperty("cancellationReason").GetString()));
        Assert.Equal((false, (string?)null, (string?)null), (logs[2].Succeeded, logs[2].OldValues, logs[2].NewValues));
    }

    [Fact]
    public async Task A_rent_change_records_the_old_and_new_monthly_rent()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");

        using var response = await client.PutAsJsonAsync("/api/properties/1/room-types/1", new { monthlyRent = 3_500_000m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .SingleAsync(item => item.Action == "PropertyRoomTypes.Update");
        Assert.Equal(3_000_000m, JsonDocument.Parse(log.OldValues!).RootElement.GetProperty("monthlyRent").GetDecimal());
        Assert.Equal(3_500_000m, JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("monthlyRent").GetDecimal());
    }

    [Fact]
    public void Recorded_values_are_camel_case_json_that_keeps_vietnamese_text_readable()
    {
        var trail = new PRN232.Application.Services.AuditTrail();

        trail.Record(new { Note = "Hư hỏng", Month = new DateOnly(2026, 11, 1) }, null);

        Assert.Equal("{\"note\":\"Hư hỏng\",\"month\":\"2026-11-01\"}", trail.OldValues);
        Assert.Null(trail.NewValues);
    }

    private sealed class BrokenWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Audit store is down.");
    }
}
