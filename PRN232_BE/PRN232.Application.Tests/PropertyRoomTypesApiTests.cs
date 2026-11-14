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

public sealed class PropertyRoomTypesApiTests
{
    private const string Password = "property-room-type-password-123";

    [Fact]
    public async Task Admin_reads_scoped_configurations_with_room_counts()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");

        using var response = await client.GetAsync("/api/properties/1/room-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadArrayAsync(response);
        Assert.Equal(2, items.Length);
        var studio = Assert.Single(items, item =>
            item.GetProperty("roomTypeName").GetString() == "Studio");
        Assert.Equal(2, studio.GetProperty("totalRoomCount").GetInt32());
        Assert.Equal(1, studio.GetProperty("availableRoomCount").GetInt32());
        Assert.Equal(3_000_000m, studio.GetProperty("monthlyRent").GetDecimal());

        using var otherPropertyResponse = await client.GetAsync("/api/properties/2/room-types");
        var otherPropertyItems = await ReadArrayAsync(otherPropertyResponse);
        Assert.Single(otherPropertyItems);
        Assert.Equal(4_000_000m,
            otherPropertyItems[0].GetProperty("monthlyRent").GetDecimal());
    }

    [Fact]
    public async Task Admin_creates_a_catalog_type_and_configures_only_the_selected_property()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");

        using var catalogResponse = await client.PostAsJsonAsync("/api/room-types", new
        {
            typeName = "  Phòng   Deluxe  ",
            price = 0m
        });

        Assert.Equal(HttpStatusCode.Created, catalogResponse.StatusCode);
        var catalogItem = await catalogResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roomTypeId = catalogItem.GetProperty("roomTypeId").GetInt32();
        Assert.Equal("Phòng Deluxe", catalogItem.GetProperty("typeName").GetString());
        Assert.Equal(0m, catalogItem.GetProperty("price").GetDecimal());

        using var configurationResponse = await client.PostAsJsonAsync(
            "/api/properties/1/room-types",
            new { roomTypeId, monthlyRent = 4_500_000m });

        Assert.Equal(HttpStatusCode.Created, configurationResponse.StatusCode);
        var configuration = await configurationResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, configuration.GetProperty("propertyId").GetInt32());
        Assert.Equal("Phòng Deluxe", configuration.GetProperty("roomTypeName").GetString());
        Assert.Equal(4_500_000m, configuration.GetProperty("monthlyRent").GetDecimal());
        Assert.Equal(0, configuration.GetProperty("totalRoomCount").GetInt32());

        using var otherPropertyResponse = await client.GetAsync("/api/properties/2/room-types");
        var otherPropertyItems = await ReadArrayAsync(otherPropertyResponse);
        Assert.DoesNotContain(otherPropertyItems,
            item => item.GetProperty("roomTypeId").GetInt32() == roomTypeId);

        using var selectedPropertyResponse = await client.GetAsync("/api/properties/1/room-types");
        var selectedPropertyItems = await ReadArrayAsync(selectedPropertyResponse);
        Assert.Contains(selectedPropertyItems,
            item => item.GetProperty("roomTypeId").GetInt32() == roomTypeId);
    }

    [Fact]
    public async Task Duplicate_catalog_name_is_rejected_case_insensitively()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");

        using var response = await client.PostAsJsonAsync("/api/room-types", new
        {
            typeName = "  sTuDiO  ",
            price = 0m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("đã tồn tại trong danh mục",
            problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Duplicate_property_configuration_is_rejected()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");

        using var response = await client.PostAsJsonAsync(
            "/api/properties/1/room-types",
            new { roomTypeId = 1, monthlyRent = 4_000_000m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_read_assigned_property_but_cannot_create_configuration()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");

        using var assignedResponse = await client.GetAsync("/api/properties/1/room-types");
        using var otherResponse = await client.GetAsync("/api/properties/2/room-types");
        Assert.Equal(HttpStatusCode.OK, assignedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/properties/1/room-types",
            new { roomTypeId = 2, monthlyRent = 4_000_000m });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_require_authentication()
    {
        using var factory = new PropertyRoomTypesApiFactory();
        using var client = factory.CreateClient();

        using var readResponse = await client.GetAsync("/api/properties/1/room-types");
        using var catalogResponse = await client.PostAsJsonAsync("/api/room-types", new
        {
            typeName = "Anonymous Type",
            price = 0m
        });
        using var configurationResponse = await client.PostAsJsonAsync(
            "/api/properties/1/room-types",
            new { roomTypeId = 2, monthlyRent = 4_000_000m });

        Assert.Equal(HttpStatusCode.Unauthorized, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, catalogResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, configurationResponse.StatusCode);
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToArray();
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = Password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
    }
}

internal sealed class PropertyRoomTypesApiFactory : WebApplicationFactory<PropertyRoomTypesController>
{
    private const string Password = "property-room-type-password-123";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
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
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = "Customer" },
            new Roles { RoleId = 2, RoleName = "Manager" },
            new Roles { RoleId = 3, RoleName = "Admin" });
        context.Users.AddRange(
            User(9, 3, "Admin", "admin@example.test", passwordHash),
            User(7, 2, "Manager", "manager@example.test", passwordHash),
            User(10, 1, "Customer", "customer@example.test", passwordHash));
        context.Properties.AddRange(
            Property(1, "Alpha"),
            Property(2, "Beta"));
        context.RoomTypes.AddRange(
            new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 0m },
            new RoomTypes { RoomTypeId = 2, TypeName = "Deluxe", Price = 0m });
        context.PropertyRoomTypes.AddRange(
            new PropertyRoomTypes
            {
                PropertyId = 1,
                RoomTypeId = 1,
                MonthlyRent = 3_000_000m,
                IsActive = true
            },
            new PropertyRoomTypes
            {
                PropertyId = 1,
                RoomTypeId = 2,
                MonthlyRent = 5_000_000m,
                IsActive = true
            },
            new PropertyRoomTypes
            {
                PropertyId = 2,
                RoomTypeId = 1,
                MonthlyRent = 4_000_000m,
                IsActive = true
            });
        context.Rooms.AddRange(
            Room(1, 1, 1, "A101", "Available"),
            Room(2, 1, 1, "A102", "Occupied"),
            Room(3, 1, 2, "B101", "Available"),
            Room(4, 2, 1, "C101", "Available"));
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

    private static Property Property(int propertyId, string name) => new()
    {
        PropertyId = propertyId,
        OwnerId = 9,
        PropertyName = name,
        AddressText = $"{propertyId} Test Street",
        Ward = "Ward 1",
        District = "District 1",
        City = "City"
    };

    private static Rooms Room(
        int roomId,
        int propertyId,
        int roomTypeId,
        string roomCode,
        string status) => new()
    {
        RoomId = roomId,
        PropertyId = propertyId,
        RoomTypeId = roomTypeId,
        RoomCode = roomCode,
        AreaSqm = 20m,
        Status = status
    };

    private static Users User(
        int userId,
        int roleId,
        string fullName,
        string email,
        string passwordHash) => new()
    {
        UserId = userId,
        RoleId = roleId,
        FullName = fullName,
        Email = email,
        PasswordHash = passwordHash,
        PhoneNumber = "0900000000",
        IsActive = true
    };
}
