using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRN232.Api.Controllers;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class EquipmentApiContractTests
{
    [Fact]
    public async Task Admin_list_returns_joined_fields_and_applies_property_filter()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.GetAsync("/api/equipments?propertyId=1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = document.RootElement.EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("A101", items[0].GetProperty("roomCode").GetString());
        Assert.Equal(1, items[0].GetProperty("propertyId").GetInt32());
        Assert.Equal("Sunrise", items[0].GetProperty("propertyName").GetString());
    }

    [Fact]
    public async Task Equipment_list_supports_optional_paging_without_changing_array_response()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.GetAsync("/api/equipments?page=2&pageSize=1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = document.RootElement.EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(102, items[0].GetProperty("equipmentId").GetInt32());
    }

    [Fact]
    public async Task Manager_list_and_detail_are_limited_to_active_assignments()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        client.DefaultRequestHeaders.Add("X-Test-User", "7");

        using var list = await client.GetAsync("/api/equipments");
        using var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        using var assignedDetail = await client.GetAsync("/api/equipments/101");
        using var outsideDetail = await client.GetAsync("/api/equipments/102");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal([101], listDocument.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("equipmentId").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, assignedDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsideDetail.StatusCode);
    }

    [Fact]
    public async Task Assigned_manager_can_create_but_cannot_use_an_unassigned_room()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        client.DefaultRequestHeaders.Add("X-Test-User", "7");

        using var assigned = await client.PostAsJsonAsync("/api/equipments", CreateRequest(1));
        using var outside = await client.PostAsJsonAsync("/api/equipments", CreateRequest(3));

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_invalid_warranty_and_status_values()
    {
        await using var api = await CreateApiAsync();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var invalidWarranty = await client.PostAsJsonAsync("/api/equipments", CreateRequest(
            1,
            installDate: "2026-01-20",
            warrantyExpiry: "2026-01-19"));
        using var invalidStatus = await client.PostAsJsonAsync("/api/equipments", CreateRequest(
            1,
            status: "Unsupported"));

        Assert.Equal(HttpStatusCode.BadRequest, invalidWarranty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_conflict_when_maintenance_history_references_equipment()
    {
        await using var api = await CreateApiAsync(includeMaintenanceRequest: true);
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.DeleteAsync("/api/equipments/101");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static object CreateRequest(
        int roomId,
        string installDate = "2026-01-15",
        string warrantyExpiry = "2028-01-15",
        string status = "Operational") => new
        {
            roomId,
            name = "Máy lạnh kiểm thử",
            installDate,
            warrantyExpiry,
            supplier = "Nhà cung cấp kiểm thử",
            status
        };

    private static async Task<TestApi> CreateApiAsync(bool includeMaintenanceRequest = false)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        builder.Services.AddScoped<IEquipmentService, EquipmentService>();
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(EquipmentsController).Assembly);

        var application = builder.Build();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();

        using var scope = application.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
        Seed(context, includeMaintenanceRequest);
        return new TestApi(application, connection);
    }

    private static void Seed(AppDbContext context, bool includeMaintenanceRequest)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = "Customer" },
            new Roles { RoleId = 2, RoleName = "Manager" },
            new Roles { RoleId = 3, RoleName = "Admin" });
        context.Users.AddRange(
            User(7, 2, "Manager A"),
            User(8, 2, "Manager B"),
            User(9, 3, "Admin"),
            User(10, 1, "Customer"));
        context.Properties.AddRange(
            Property(1, 9, "Sunrise"),
            Property(2, 9, "Sunset"),
            Property(3, 7, "Legacy owner property"));
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.AddRange(
            Room(1, 1, "A101"),
            Room(2, 2, "B101"),
            Room(3, 3, "C101"));
        context.Equipments.AddRange(
            Equipment(101, 1, "Sunrise AC"),
            Equipment(102, 2, "Sunset AC"),
            Equipment(103, 3, "Legacy AC"));
        context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 });
        if (includeMaintenanceRequest)
        {
            context.Contracts.Add(new Contracts
            {
                ContractId = 1,
                RoomId = 1,
                CustomerId = 10,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2027, 1, 1),
                MonthlyRent = 3_000_000m,
                DepositAmount = 3_000_000m,
                Status = "Active"
            });
            context.MaintenanceRequests.Add(new MaintenanceRequests
            {
                RequestId = 1,
                ContractId = 1,
                EquipmentId = 101,
                Description = "Máy lạnh hỏng",
                Status = "Pending"
            });
        }
        context.SaveChanges();
    }

    private static Users User(int id, int roleId, string name) => new()
    {
        UserId = id,
        RoleId = roleId,
        FullName = name,
        Email = $"user-{id}@example.test",
        PasswordHash = "unused",
        PhoneNumber = "0900000000",
        IsActive = true
    };

    private static Property Property(int id, int ownerId, string name) => new()
    {
        PropertyId = id,
        OwnerId = ownerId,
        PropertyName = name,
        AddressText = "Address",
        Ward = "Ward",
        District = "District",
        City = "City"
    };

    private static Rooms Room(int id, int propertyId, string code) => new()
    {
        RoomId = id,
        PropertyId = propertyId,
        RoomTypeId = 1,
        RoomCode = code,
        AreaSqm = 20,
        Status = "Available"
    };

    private static Equipments Equipment(int id, int roomId, string name) => new()
    {
        EquipmentId = id,
        RoomId = roomId,
        Name = name,
        InstallDate = new DateOnly(2026, 1, 15),
        WarrantyExpiry = new DateOnly(2028, 1, 15),
        Supplier = "Supplier",
        Status = "Operational"
    };

    private sealed class TestApi(WebApplication application, SqliteConnection connection) : IAsyncDisposable
    {
        public HttpClient CreateClient() => application.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            await application.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var userId = Request.Headers["X-Test-User"].FirstOrDefault() ?? "9";
            var identity = new ClaimsIdentity(
                [
                    new Claim("sub", userId),
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, role)
                ],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
