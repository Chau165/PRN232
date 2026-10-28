using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PRN232.Api.Controllers;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class MyRentalsApiTests
{
    private static readonly DateOnly Today = new(2026, 1, 1);
    private const string Password = "rental-test-password";

    [Fact]
    public async Task Requires_authentication_and_customer_role()
    {
        using var factory = new RentalsApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/me/rentals")).StatusCode);
        using (var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json")))
        {
            var getOperation = swagger.RootElement.GetProperty("paths")
                .GetProperty("/api/me/rentals")
                .GetProperty("get");
            Assert.True(getOperation.TryGetProperty("security", out _));
        }

        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/me/rentals")).StatusCode);

        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/me/rentals")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateCustomerTokenWithoutUserId());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/me/rentals")).StatusCode);
    }

    [Fact]
    public async Task Returns_empty_array_for_customer_without_current_rentals()
    {
        using var factory = new RentalsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "empty@example.test");

        using var response = await client.GetAsync("/api/me/rentals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Returns_owned_active_rentals_and_room_scoped_related_data()
    {
        using var factory = new RentalsApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        factory.SqlCapture.Commands.Clear();

        using var response = await client.GetAsync("/api/me/rentals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rental = Assert.Single(body.RootElement.EnumerateArray());

        Assert.Equal(1, rental.GetProperty("contractId").GetInt32());
        Assert.Equal("Active", rental.GetProperty("contractStatus").GetString());
        Assert.Equal("2025-12-01", rental.GetProperty("startDate").GetString());
        Assert.Equal("2026-02-01", rental.GetProperty("endDate").GetString());
        Assert.Equal(3_800_000m, rental.GetProperty("monthlyRent").GetDecimal());
        Assert.Equal(7_600_000m, rental.GetProperty("depositAmount").GetDecimal());
        Assert.Equal(101, rental.GetProperty("roomId").GetInt32());
        Assert.Equal("A203", rental.GetProperty("roomCode").GetString());
        Assert.Equal("Maintenance", rental.GetProperty("roomStatus").GetString());
        Assert.Equal(24m, rental.GetProperty("areaSqm").GetDecimal());
        Assert.Equal(1, rental.GetProperty("roomTypeId").GetInt32());
        Assert.Equal("Studio", rental.GetProperty("roomTypeName").GetString());
        Assert.Equal(1, rental.GetProperty("propertyId").GetInt32());
        Assert.Equal("Sunrise Residence", rental.GetProperty("propertyName").GetString());
        Assert.Equal("45 Vo Van Ngan", rental.GetProperty("propertyAddress").GetString());
        Assert.Equal("https://images.test/primary-first.jpg", rental.GetProperty("primaryImageUrl").GetString());
        Assert.Equal(new[]
        {
            "https://images.test/other.jpg",
            "https://images.test/primary-first.jpg",
            "https://images.test/primary-second.jpg"
        }, rental.GetProperty("images").EnumerateArray().Select(image => image.GetString()));

        var equipment = Assert.Single(rental.GetProperty("equipments").EnumerateArray());
        Assert.Equal(11, equipment.GetProperty("equipmentId").GetInt32());
        Assert.Equal("Air conditioner", equipment.GetProperty("name").GetString());
        Assert.Equal("NeedsRepair", equipment.GetProperty("status").GetString());
        var amenity = Assert.Single(rental.GetProperty("amenities").EnumerateArray());
        Assert.Equal(21, amenity.GetProperty("amenityId").GetInt32());
        Assert.Equal("Wi-Fi", amenity.GetProperty("name").GetString());

        var json = body.RootElement.GetRawText();
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Supplier", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FullName", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, factory.SqlCapture.Commands.Count);
    }

    [Fact]
    public async Task Returns_all_current_contracts_and_never_another_customers_rental()
    {
        using var factory = new RentalsApiFactory();
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.Add(new Contracts
            {
                ContractId = 8,
                CustomerId = 10,
                RoomId = 103,
                Status = "Active",
                StartDate = Today,
                EndDate = Today,
                MonthlyRent = 4_100_000m,
                DepositAmount = 8_200_000m
            });
            context.Contracts.AddRange(
                Contract(9, 10, 104, "Active", Today, Today),
                Contract(10, 10, 105, "Active", Today, Today),
                Contract(11, 10, 101, "Active", Today, Today));
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        factory.SqlCapture.Commands.Clear();
        using var response = await client.GetAsync("/api/me/rentals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { 8, 9, 10, 11, 1 }, body.RootElement.EnumerateArray()
            .Select(rental => rental.GetProperty("contractId").GetInt32()));
        Assert.DoesNotContain(body.RootElement.EnumerateArray(), rental =>
            rental.GetProperty("contractId").GetInt32() == 2);
        Assert.Equal(4, factory.SqlCapture.Commands.Count);
    }

    [Fact]
    public async Task Rental_history_contains_only_expired_active_contracts_owned_by_customer()
    {
        using var factory = new RentalsApiFactory();
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.AddRange(
                Contract(8, 10, 103, "Active", Today.AddYears(-1), Today.AddDays(-1)),
                Contract(9, 10, 104, "Active", Today.AddDays(1), Today.AddDays(30)),
                Contract(10, 10, 105, "Cancelled", Today.AddYears(-1), Today.AddDays(-1)),
                Contract(11, 20, 102, "Active", Today.AddYears(-1), Today.AddDays(-1)));
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var response = await client.GetAsync("/api/me/rentals/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rental = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(8, rental.GetProperty("contractId").GetInt32());
        Assert.Equal("C305", rental.GetProperty("roomCode").GetString());
    }

    [Fact]
    public async Task Rental_history_requires_customer_role()
    {
        using var factory = new RentalsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/me/rentals/history")).StatusCode);
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/me/rentals/history")).StatusCode);
    }

    [Fact]
    public async Task Rental_history_uses_vietnam_date_for_expiry_boundary()
    {
        using var factory = new RentalsApiFactory(new DateTimeOffset(2026, 1, 1, 16, 59, 0, TimeSpan.Zero));
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.Add(Contract(8, 10, 103, "Active", Today.AddYears(-1), Today));
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var beforeVietnamMidnight = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals/history");
        Assert.Empty(beforeVietnamMidnight.EnumerateArray());
        factory.Clock.UtcNow = new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.Zero);
        var afterVietnamMidnight = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals/history");
        Assert.Single(afterVietnamMidnight.EnumerateArray());
    }

    [Fact]
    public async Task Excludes_future_expired_and_non_active_contracts()
    {
        using var factory = new RentalsApiFactory();
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.AddRange(
                Contract(8, 10, 103, "Active", Today.AddDays(1), Today.AddDays(3)),
                Contract(9, 10, 104, "Active", Today.AddDays(-3), Today.AddDays(-1)),
                Contract(10, 10, 105, "Pending", Today.AddDays(-1), Today.AddDays(1)));
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var rentals = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals");
        Assert.Equal(new[] { 1 }, rentals.EnumerateArray()
            .Select(rental => rental.GetProperty("contractId").GetInt32()));
    }

    [Fact]
    public async Task Applies_inclusive_boundaries_using_vietnam_business_date()
    {
        using var factory = new RentalsApiFactory(
            new DateTimeOffset(2026, 1, 1, 16, 59, 0, TimeSpan.Zero));
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contract = await context.Contracts.SingleAsync(contract => contract.ContractId == 1);
            contract.StartDate = Today;
            contract.EndDate = Today;
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        var beforeMidnight = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals");
        Assert.Single(beforeMidnight.EnumerateArray());

        factory.Clock.UtcNow = new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.Zero);
        var afterMidnight = await client.GetFromJsonAsync<JsonElement>("/api/me/rentals");
        Assert.Empty(afterMidnight.EnumerateArray());
    }

    [Fact]
    public async Task Returns_empty_image_array_and_uses_property_image_fallback()
    {
        using var factory = new RentalsApiFactory();
        factory.EnsureSeeded();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Contracts.Add(new Contracts
            {
                ContractId = 8,
                CustomerId = 10,
                RoomId = 103,
                Status = "Active",
                StartDate = Today,
                EndDate = Today,
                MonthlyRent = 2_900_000m,
                DepositAmount = 5_800_000m
            });
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");

        using var body = JsonDocument.Parse(await (await client.GetAsync("/api/me/rentals")).Content.ReadAsStringAsync());
        var rental = Assert.Single(body.RootElement.EnumerateArray(),
            item => item.GetProperty("roomId").GetInt32() == 103);
        Assert.Empty(rental.GetProperty("images").EnumerateArray());
        Assert.Equal("https://images.test/property.jpg", rental.GetProperty("primaryImageUrl").GetString());
        Assert.Empty(rental.GetProperty("equipments").EnumerateArray());
        Assert.Empty(rental.GetProperty("amenities").EnumerateArray());
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("accessToken").GetString());
    }

    private static string CreateCustomerTokenWithoutUserId()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-only-signing-key-32-bytes"));
        var token = new JwtSecurityToken(
            "test-issuer",
            "test-audience",
            [new Claim(ClaimTypes.Role, "Customer")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Contracts Contract(
        int id,
        int customerId,
        int roomId,
        string status,
        DateOnly startDate,
        DateOnly endDate) => new()
    {
        ContractId = id,
        CustomerId = customerId,
        RoomId = roomId,
        Status = status,
        StartDate = startDate,
        EndDate = endDate,
        MonthlyRent = 3_000_000m,
        DepositAmount = 6_000_000m
    };

    private sealed class RentalsApiFactory : WebApplicationFactory<MyRentalsController>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private bool _seeded;
        public RentalSqlCapture SqlCapture { get; } = new();
        public MutableTimeProvider Clock { get; }

        public RentalsApiFactory(DateTimeOffset? utcNow = null) =>
            Clock = new MutableTimeProvider(utcNow ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
            }));
            builder.ConfigureTestServices(services =>
            {
                _connection.Open();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddDbContext<AppDbContext>(options => options
                    .UseSqlite(_connection)
                    .AddInterceptors(SqlCapture));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            EnsureSeeded();
        }

        public void EnsureSeeded()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
            if (_seeded) return;
            Seed(context);
            _seeded = true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }

        private static void Seed(AppDbContext context)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
            context.Roles.AddRange(
                new Roles { RoleId = 1, RoleName = "Customer" },
                new Roles { RoleId = 2, RoleName = "Manager" },
                new Roles { RoleId = 3, RoleName = "Admin" });
            context.Users.AddRange(
                User(10, 1, "Customer", "customer@example.test", hash),
                User(20, 1, "Other Customer", "other@example.test", hash),
                User(11, 1, "Empty Customer", "empty@example.test", hash),
                User(7, 2, "Manager", "manager@example.test", hash),
                User(9, 3, "Admin", "admin@example.test", hash));
            context.Properties.Add(new Property
            {
                PropertyId = 1,
                OwnerId = 7,
                PropertyName = "Sunrise Residence",
                AddressText = "45 Vo Van Ngan",
                PrimaryImageUrl = "https://images.test/property.jpg"
            });
            context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 500_000m });
            context.Rooms.AddRange(
                Room(101, "A203", "Maintenance"),
                Room(102, "B204", "Occupied"),
                Room(103, "C305", "Available"),
                Room(104, "D406", "Available"),
                Room(105, "E507", "Available"));
            context.Amenities.Add(new Amenities { AmenityId = 21, Name = "Wi-Fi" });
            context.RoomImages.AddRange(
                new RoomImages { RoomImageId = 1, RoomId = 101, ImageUrl = "https://images.test/other.jpg" },
                new RoomImages { RoomImageId = 2, RoomId = 101, IsPrimary = true, ImageUrl = "https://images.test/primary-first.jpg" },
                new RoomImages { RoomImageId = 3, RoomId = 101, IsPrimary = true, ImageUrl = "https://images.test/primary-second.jpg" },
                new RoomImages { RoomImageId = 4, RoomId = 102, IsPrimary = true, ImageUrl = "https://images.test/other-room.jpg" });
            context.Equipments.AddRange(
                Equipment(11, 101, "Air conditioner", "NeedsRepair"),
                Equipment(12, 102, "Other room equipment", "Working"));
            context.RoomAmenities.AddRange(
                new RoomAmenities { RoomId = 101, AmenityId = 21 },
                new RoomAmenities { RoomId = 102, AmenityId = 21 });
            context.Contracts.AddRange(
                new Contracts
                {
                    ContractId = 1, CustomerId = 10, RoomId = 101, Status = "Active",
                    StartDate = Today.AddMonths(-1), EndDate = Today.AddMonths(1),
                    MonthlyRent = 3_800_000m, DepositAmount = 7_600_000m
                },
                Contract(2, 20, 102, "Active", Today.AddMonths(-1), Today.AddMonths(1)));
            context.SaveChanges();
        }

        private static Users User(int id, int roleId, string name, string email, string passwordHash) => new()
        {
            UserId = id,
            RoleId = roleId,
            FullName = name,
            Email = email,
            PasswordHash = passwordHash,
            PhoneNumber = "0000000000",
            IsActive = true
        };

        private static Rooms Room(int id, string code, string status) => new()
        {
            RoomId = id,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = code,
            AreaSqm = 24m,
            Status = status
        };

        private static Equipments Equipment(int id, int roomId, string name, string status) => new()
        {
            EquipmentId = id,
            RoomId = roomId,
            Name = name,
            Status = status,
            InstallDate = Today.AddYears(-1),
            WarrantyExpiry = Today.AddYears(1),
            Supplier = "Private supplier detail"
        };
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    public sealed class RentalSqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
