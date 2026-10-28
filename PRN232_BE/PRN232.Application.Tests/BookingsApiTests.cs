using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Api.Controllers;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingsApiTests
{
    internal const string Password = "test-password-123";

    [Theory]
    [InlineData("GET", "/api/bookings/1")]
    [InlineData("POST", "/api/bookings")]
    [InlineData("POST", "/api/bookings/1/pay")]
    [InlineData("GET", "/api/bookings")]
    [InlineData("POST", "/api/bookings/1/cancel")]
    public async Task Anonymous_requests_require_authentication(string method, string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Only_customers_can_create_bookings()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");

        var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_creates_pending_booking_and_cannot_spoof_owner_status_or_amount()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var response = await client.PostAsJsonAsync("/api/bookings", new
        { roomId = 1, customerId = 20, status = "Deposited", depositAmount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = body.GetProperty("bookingId").GetInt32();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(10, body.GetProperty("customerId").GetInt32());
        Assert.Equal(3_000_000m, body.GetProperty("depositAmount").GetDecimal());
        Assert.Equal("A101", body.GetProperty("roomCode").GetString());
        Assert.EndsWith($"/api/bookings/{bookingId}", response.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.SingleAsync();
        Assert.Equal((10, "Pending"), (saved.CustomerId, saved.Status));
    }

    [Fact]
    public async Task Room_held_by_pending_booking_cannot_be_booked_by_another_customer()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 })).StatusCode);

        await LoginAsync(client, "other@example.test");
        var second = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData(2, HttpStatusCode.Conflict)]
    [InlineData(999, HttpStatusCode.NotFound)]
    public async Task Unavailable_or_unknown_rooms_are_rejected(int roomId, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var response = await client.PostAsJsonAsync("/api/bookings", new { roomId });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", HttpStatusCode.OK)]
    [InlineData("other@example.test", HttpStatusCode.Forbidden)]
    [InlineData("manager@example.test", HttpStatusCode.OK)]
    [InlineData("manager-b@example.test", HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", HttpStatusCode.OK)]
    public async Task Booking_detail_follows_customer_owner_and_admin_scope(string email, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        await LoginAsync(client, email);
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Customer_pays_deposit_through_mock_gateway_and_owner_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "MoMo" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Deposited", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("expiryDate").GetDateTime() > DateTime.UtcNow.AddDays(6));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.SingleAsync();
        Assert.Equal((10, (int?)bookingId, 3_000_000m, "MoMo", "Success"),
            (payment.UserId, payment.BookingId, payment.Amount, payment.PaymentMethod, payment.Status));
        var notification = await context.Notifications.SingleAsync();
        Assert.Equal((7, "Booking", false), (notification.UserId, notification.Type, notification.IsRead));
    }

    [Fact]
    public async Task Paid_booking_cannot_be_paid_again()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" })).StatusCode);

        var second = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.CountAsync());
    }

    [Theory]
    [InlineData("other@example.test")]
    [InlineData("manager@example.test")]
    public async Task Only_the_booking_customer_can_pay(string email)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_payment_method_or_unknown_booking_is_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        var cash = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "Cash" });
        var unknown = await client.PostAsJsonAsync("/api/bookings/999/pay", new { paymentMethod = "VNPay" });

        Assert.Equal(HttpStatusCode.BadRequest, cash.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", 1)]
    [InlineData("other@example.test", 0)]
    [InlineData("manager@example.test", 1)]
    [InlineData("manager-b@example.test", 0)]
    [InlineData("admin@example.test", 1)]
    public async Task Booking_list_follows_customer_owner_and_admin_scope(string email, int expectedCount)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        await CreateBookingAsync(client);

        await LoginAsync(client, email);
        var bookings = await client.GetFromJsonAsync<JsonElement>("/api/bookings");

        Assert.Equal(expectedCount, bookings.GetArrayLength());
    }

    [Fact]
    public async Task Booking_list_filters_by_status_and_rejects_unknown_status()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        await CreateBookingAsync(client);

        var pending = await client.GetFromJsonAsync<JsonElement>("/api/bookings?status=Pending");
        var deposited = await client.GetFromJsonAsync<JsonElement>("/api/bookings?status=Deposited");
        var unknown = await client.GetAsync("/api/bookings?status=Expired");

        Assert.Equal((1, 0), (pending.GetArrayLength(), deposited.GetArrayLength()));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Customer_cancels_unpaid_booking_and_room_is_released()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        var response = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/bookings/{bookingId}/cancel", null)).StatusCode);
        await LoginAsync(client, "other@example.test");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 })).StatusCode);
    }

    [Fact]
    public async Task Paid_booking_is_cancelled_by_owner_not_customer_and_customer_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);
        await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new { paymentMethod = "VNPay" });

        var byCustomer = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);
        await LoginAsync(client, "manager-b@example.test");
        var byOtherManager = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);
        await LoginAsync(client, "manager@example.test");
        var byOwner = await client.PostAsync($"/api/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, byCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Cancelled", (await context.Bookings.SingleAsync()).Status);
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10));
    }

    private static async Task<int> CreateBookingAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("bookingId").GetInt32();
    }

    internal static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }
}

internal sealed class BookingsApiFactory : WebApplicationFactory<BookingsController>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes", ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience", ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
        }));
        builder.ConfigureTestServices(services =>
        {
            _connection.Open();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        var hash = BCrypt.Net.BCrypt.HashPassword(BookingsApiTests.Password, workFactor: 4);
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = "Customer" },
            new Roles { RoleId = 2, RoleName = "Manager" },
            new Roles { RoleId = 3, RoleName = "Admin" });
        context.Users.AddRange(
            User(10, 1, "Customer A", "customer@example.test", hash),
            User(20, 1, "Customer B", "other@example.test", hash),
            User(7, 2, "Manager A", "manager@example.test", hash),
            User(8, 2, "Manager B", "manager-b@example.test", hash),
            User(9, 3, "Admin", "admin@example.test", hash));
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 7, PropertyName = "Sunrise" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", Status = "Available" },
            new Rooms { RoomId = 2, PropertyId = 1, RoomTypeId = 1, RoomCode = "A102", Status = "Occupied" });
        context.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private static Users User(int id, int roleId, string name, string email, string hash) =>
        new() { UserId = id, RoleId = roleId, FullName = name, Email = email, PasswordHash = hash, IsActive = true };
}
