using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// B4: a customer can hold only one unpaid (Pending, not expired) booking at a time.
public sealed class BookingPendingLimitTests
{
    [Fact]
    public async Task Second_booking_while_the_first_is_pending_returns_409_with_the_pending_id()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await AddSecondRentableRoomAsync(factory);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var firstId = await BookAsync(client, roomId: 1);

        using var second = await client.PostAsJsonAsync("/api/bookings", new { roomId = 3 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(BookingCustomerHasPendingBookingException.DefaultMessage,
            body.RootElement.GetProperty("detail").GetString());
        Assert.Equal(firstId, body.RootElement.GetProperty("pendingBookingId").GetInt32());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.Bookings.AnyAsync(booking => booking.RoomId == 3));
    }

    [Fact]
    public async Task Booking_is_allowed_again_after_the_pending_one_is_cancelled()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await AddSecondRentableRoomAsync(factory);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var firstId = await BookAsync(client, roomId: 1);

        using var cancel = await client.PostAsync($"/api/bookings/{firstId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/bookings", new { roomId = 3 });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Expired_pending_booking_does_not_count()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await AddSecondRentableRoomAsync(factory);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var firstId = await BookAsync(client, roomId: 1);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var first = await context.Bookings.SingleAsync(booking => booking.BookingId == firstId);
            first.ExpiryDate = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        using var second = await client.PostAsJsonAsync("/api/bookings", new { roomId = 3 });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Deposited_booking_does_not_count()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await AddSecondRentableRoomAsync(factory);
        var firstId = await ContractsApiTests.CreatePaidBookingAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Deposited", (await context.Bookings.SingleAsync(booking => booking.BookingId == firstId)).Status);
        }

        using var second = await client.PostAsJsonAsync("/api/bookings", new { roomId = 3 });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Another_customer_is_not_affected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await AddSecondRentableRoomAsync(factory);
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        await BookAsync(client, roomId: 1);

        await BookingsApiTests.LoginAsync(client, "other@example.test");
        using var other = await client.PostAsJsonAsync("/api/bookings", new { roomId = 3 });

        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    // The shared factory seeds data when the client is created, so add room 3 afterwards.
    private static async Task AddSecondRentableRoomAsync(BookingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Rooms.Add(new Rooms { RoomId = 3, PropertyId = 1, RoomTypeId = 1, RoomCode = "A103", Status = "Available" });
        await context.SaveChangesAsync();
    }

    private static async Task<int> BookAsync(HttpClient client, int roomId)
    {
        using var response = await client.PostAsJsonAsync("/api/bookings", new { roomId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bookingId").GetInt32();
    }
}
