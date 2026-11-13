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
using PRN232.Api.Hubs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingsApiTests
{
    internal const string Password = "test-password-123";

    [Fact]
    public async Task Anonymous_cannot_connect_to_notification_hub()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_hub_connection_uses_the_jwt_user_identity()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_query_token_is_accepted_only_for_the_notification_hub()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var token = client.DefaultRequestHeaders.Authorization!.Parameter;
        client.DefaultRequestHeaders.Authorization = null;

        using var hubResponse = await client.PostAsync($"/hubs/notifications/negotiate?negotiateVersion=1&access_token={token}", null);
        using var apiResponse = await client.GetAsync($"/api/bookings?access_token={token}");

        Assert.Equal(HttpStatusCode.OK, hubResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, apiResponse.StatusCode);
    }

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

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_creates_pending_booking_and_cannot_spoof_owner_status_or_amount()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1,
            customerId = 20,
            status = "Deposited",
            depositAmount = 1
        });

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
        var pendingPush = Assert.Single(factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>().Created);
        Assert.Equal(9, pendingPush.UserId);
        Assert.Contains("chờ thanh toán cọc", pendingPush.Title);
    }

    [Fact]
    public async Task Room_held_by_pending_booking_cannot_be_booked_by_another_customer()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        })).StatusCode);

        await LoginAsync(client, "other@example.test");
        var second = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        });

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

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId
        });

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>().Created);
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

        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "MoMo"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Deposited", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("expiryDate").GetDateTime() > DateTime.UtcNow.AddDays(6));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.SingleAsync();
        Assert.NotNull(payment.PaidAtUtc);
        Assert.Equal((10, (int?)bookingId, 3_000_000m, "MoMo", "Success"),
            (payment.UserId, payment.BookingId, payment.Amount, payment.PaymentMethod, payment.Status));
        var notifications = await context.Notifications.OrderBy(notification => notification.NotificationId).ToListAsync();
        Assert.Collection(notifications,
            pending =>
            {
                Assert.Equal((9, "Booking", false), (pending.UserId, pending.Type, pending.IsRead));
                Assert.Contains("chờ thanh toán cọc", pending.Title);
            },
            deposited =>
            {
                Assert.Equal((9, "Booking", false), (deposited.UserId, deposited.Type, deposited.IsRead));
                Assert.Contains("đã được đặt cọc", deposited.Title);
            });
        var pushed = factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>().Created;
        Assert.Equal(2, pushed.Count);
        Assert.All(pushed, notification => Assert.Equal(9, notification.UserId));
    }

    [Fact]
    public async Task Paid_booking_cannot_be_paid_again()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
            {
                paymentMethod = "VNPay"
            })).StatusCode);

        var second = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "VNPay"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(2, factory.Services.GetRequiredService<CapturingNotificationRealtimePublisher>().Created.Count);
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
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "VNPay"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_payment_method_or_unknown_booking_is_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);

        var cash = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "Cash"
        });
        var unknown = await client.PostAsJsonAsync("/api/bookings/999/pay", new
        {
            paymentMethod = "VNPay"
        });

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
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        })).StatusCode);
    }

    [Fact]
    public async Task Paid_booking_is_cancelled_by_owner_not_customer_and_customer_is_notified()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var bookingId = await CreateBookingAsync(client);
        await client.PostAsJsonAsync($"/api/bookings/{bookingId}/pay", new
        {
            paymentMethod = "VNPay"
        });

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
        Assert.Equal(2, await context.Notifications.CountAsync(notification => notification.UserId == 9));
        Assert.Equal(1, await context.Notifications.CountAsync(notification => notification.UserId == 10));
    }

    private static async Task<int> CreateBookingAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = 1
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("bookingId").GetInt32();
    }

    internal static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = Password
        });
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
            ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["PayOS:Enabled"] = "false",
            ["PayOS:BookingPaymentsEnabled"] = "false",
            ["PayOSPayout:Enabled"] = "false",
            ["PayOSPayout:DispatchEnabled"] = "false",
            ["PayOSPayout:ReconciliationEnabled"] = "false",
            ["PayOSPayout:RetryFailedPayouts"] = "false",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.UseSharedTestDataProtection();
            _connection.Open();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<INotificationRealtimePublisher>();
            services.AddSingleton<CapturingNotificationRealtimePublisher>();
            services.AddSingleton<INotificationRealtimePublisher>(provider =>
                provider.GetRequiredService<CapturingNotificationRealtimePublisher>());
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
            User(7, 2, "Manager A", "manager@example.test", hash, phoneNumber: "0911000000"),
            User(8, 2, "Manager B", "manager-b@example.test", hash, phoneNumber: "0912000000"),
            User(14, 2, "Inactive Manager", "inactive-manager@example.test", hash, isActive: false),
            User(9, 3, "Admin", "admin@example.test", hash),
            User(11, 3, "Admin B", "admin-b@example.test", hash),
            User(12, 3, "Inactive Admin", "inactive-admin@example.test", hash, isActive: false));
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Sunrise" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", Status = "Available" },
            new Rooms { RoomId = 2, PropertyId = 1, RoomTypeId = 1, RoomCode = "A102", Status = "Occupied" });
        // Biểu giá cho test hoá đơn: điện và nước tính theo chỉ số, internet cố định theo phòng.
        context.Services.AddRange(
            new Service { ServiceId = 1, Name = "Electricity - per kWh" },
            new Service { ServiceId = 2, Name = "Water - per m3" },
            new Service { ServiceId = 3, Name = "Internet - monthly" });
        context.RoomTypeServices.AddRange(
            new RoomTypeServices { RoomTypeId = 1, ServiceId = 1 },
            new RoomTypeServices { RoomTypeId = 1, ServiceId = 2 },
            new RoomTypeServices { RoomTypeId = 1, ServiceId = 3 });
        context.PropertyServiceRates.AddRange(
            Rate(1, 1, 3_500m, "kwh", "usage"),
            Rate(2, 2, 20_000m, "m3", "usage"),
            Rate(3, 3, 100_000m, "room_month", "fixed_room"));
        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            PropertyId = 1,
            ManagerId = 7
        });
        context.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private static PropertyServiceRates Rate(int rateId, int serviceId, decimal price, string unitCode, string billingBasis) =>
        new()
        {
            RateId = rateId,
            PropertyId = 1,
            ServiceId = serviceId,
            Price = price,
            UnitCode = unitCode,
            BillingBasis = billingBasis,
            EffectiveFrom = new DateOnly(2000, 1, 1)
        };

    private static Users User(int id, int roleId, string name, string email, string hash, bool isActive = true, string phoneNumber = "") =>
        new()
        {
            UserId = id,
            RoleId = roleId,
            FullName = name,
            Email = email,
            PasswordHash = hash,
            PhoneNumber = phoneNumber,
            IsActive = isActive
        };
}

internal sealed class CapturingNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public List<PRN232.Domain.Entities.Notifications> Created { get; } = [];
    public List<(int UserId, int NotificationId, bool IsRead)> ReadUpdates { get; } = [];
    public Func<IReadOnlyCollection<PRN232.Domain.Entities.Notifications>, Task>? OnCreatedAsync { get; set; }

    public async Task PublishCreatedAsync(IEnumerable<PRN232.Domain.Entities.Notifications> notifications)
    {
        var batch = notifications.ToArray();
        Created.AddRange(batch);
        if (OnCreatedAsync is not null)
        {
            await OnCreatedAsync(batch);
        }
    }

    public Task PublishReadUpdatedAsync(int userId, int notificationId, bool isRead)
    {
        ReadUpdates.Add((userId, notificationId, isRead));
        return Task.CompletedTask;
    }
}
