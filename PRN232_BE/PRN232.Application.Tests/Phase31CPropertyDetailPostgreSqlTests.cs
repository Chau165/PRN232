using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PRN232.Api.Controllers;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class Phase31CPropertyDetailPostgreSqlTests
{
    private const string LegacySchemaTarget = "20261019034905_AddPropertyPrimaryImageUrl";

    [Phase31BPostgreSqlFact]
    public async Task Enriched_detail_uses_migrated_postgresql_and_property_scoped_rates()
    {
        var serverConnectionString = GetIsolatedServerConnectionString();
        var databaseName = NewDatabaseName("enriched");
        await CreateDatabaseAsync(serverConnectionString, databaseName);

        try
        {
            var databaseConnectionString = WithDatabase(serverConnectionString, databaseName);
            await MigrateAsync(databaseConnectionString, targetMigration: null);
            await SeedMigratedFixtureAsync(databaseConnectionString);

            using var factory = new PublicBrowsingPostgreSqlApiFactory(
                databaseConnectionString,
                propertyDetailEnrichmentEnabled: true,
                new MutableTimeProvider(new DateTimeOffset(2026, 10, 31, 16, 59, 0, TimeSpan.Zero)));
            using var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                Assert.Equal("True", configuration["PublicBrowsing:PropertyDetailEnrichmentEnabled"]);
            }

            factory.SqlCapture.Commands.Clear();
            using var alphaResponse = await client.GetAsync("/api/public/properties/1");
            Assert.Equal(HttpStatusCode.OK, alphaResponse.StatusCode);
            Assert.Equal(5, factory.SqlCapture.Commands.Count);
            using var alpha = JsonDocument.Parse(await alphaResponse.Content.ReadAsStringAsync());
            var alphaDetail = alpha.RootElement;
            Assert.Equal(2, alphaDetail.GetProperty("availableRoomCount").GetInt32());
            Assert.Equal(1_500_000m, alphaDetail.GetProperty("startingPrice").GetDecimal());
            Assert.Equal("Verified Alpha description", alphaDetail.GetProperty("description").GetString());
            Assert.Equal(new[] { 1 }, alphaDetail.GetProperty("commonAmenities").EnumerateArray()
                .Select(item => item.GetProperty("amenityId").GetInt32()));
            var roomAmenity = Assert.Single(alphaDetail.GetProperty("roomAmenities").EnumerateArray());
            Assert.Equal(3, roomAmenity.GetProperty("amenityId").GetInt32());
            Assert.Equal(2, roomAmenity.GetProperty("rentableRoomCount").GetInt32());
            Assert.Equal(new[] { 11 }, alphaDetail.GetProperty("livingCosts").EnumerateArray()
                .Select(item => item.GetProperty("serviceId").GetInt32()));
            var octoberRate = Assert.Single(alphaDetail.GetProperty("livingCosts").EnumerateArray());
            Assert.Equal(120_000m, octoberRate.GetProperty("price").GetDecimal());
            Assert.Equal("room_month", octoberRate.GetProperty("unitCode").GetString());
            Assert.Equal("fixed_room", octoberRate.GetProperty("billingBasis").GetString());
            Assert.Equal("2026-10-01", octoberRate.GetProperty("effectiveFrom").GetString());
            Assert.Equal("2026-11-01", octoberRate.GetProperty("effectiveTo").GetString());
            Assert.False(alphaDetail.TryGetProperty("ownerId", out _));
            Assert.False(alphaDetail.TryGetProperty("customerId", out _));

            Assert.Equal(5, factory.SqlCapture.Commands.Count);
            var enrichedSql = string.Join(Environment.NewLine, factory.SqlCapture.Commands);
            Assert.Contains("Description", enrichedSql, StringComparison.Ordinal);
            Assert.Contains("PropertyAmenities", enrichedSql, StringComparison.Ordinal);
            Assert.Contains("PropertyServiceRates", enrichedSql, StringComparison.Ordinal);
            Assert.DoesNotContain("OwnerId", enrichedSql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CustomerId", enrichedSql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Services\"." + "Price", enrichedSql, StringComparison.Ordinal);

            using var betaResponse = await client.GetAsync("/api/public/properties/2");
            Assert.Equal(HttpStatusCode.OK, betaResponse.StatusCode);
            using var beta = JsonDocument.Parse(await betaResponse.Content.ReadAsStringAsync());
            Assert.Equal("Verified Beta description", beta.RootElement.GetProperty("description").GetString());
            Assert.Equal(new[] { 2 }, beta.RootElement.GetProperty("commonAmenities").EnumerateArray()
                .Select(item => item.GetProperty("amenityId").GetInt32()));
            Assert.Equal(180_000m, Assert.Single(beta.RootElement.GetProperty("livingCosts").EnumerateArray())
                .GetProperty("price").GetDecimal());

            using var emptyResponse = await client.GetAsync("/api/public/properties/3");
            Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
            using var empty = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null, empty.RootElement.GetProperty("description").ValueKind);
            Assert.Empty(empty.RootElement.GetProperty("commonAmenities").EnumerateArray());
            Assert.Empty(empty.RootElement.GetProperty("roomAmenities").EnumerateArray());
            Assert.Empty(empty.RootElement.GetProperty("livingCosts").EnumerateArray());

            using var unknownResponse = await client.GetAsync("/api/public/properties/999");
            using var unavailableResponse = await client.GetAsync("/api/public/properties/4");
            Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unavailableResponse.StatusCode);
            using var unknown = JsonDocument.Parse(await unknownResponse.Content.ReadAsStringAsync());
            using var unavailable = JsonDocument.Parse(await unavailableResponse.Content.ReadAsStringAsync());
            Assert.Equal(unknown.RootElement.GetProperty("type").GetString(), unavailable.RootElement.GetProperty("type").GetString());
            Assert.Equal(unknown.RootElement.GetProperty("title").GetString(), unavailable.RootElement.GetProperty("title").GetString());

            factory.SqlCapture.Commands.Clear();
            using var roomResponse = await client.GetAsync("/api/public/rooms/101");
            Assert.Equal(HttpStatusCode.OK, roomResponse.StatusCode);
            using var room = JsonDocument.Parse(await roomResponse.Content.ReadAsStringAsync());
            var legacyService = Assert.Single(room.RootElement.GetProperty("services").EnumerateArray());
            Assert.Equal(100_000m, legacyService.GetProperty("price").GetDecimal());
            Assert.NotEqual(octoberRate.GetProperty("price").GetDecimal(), legacyService.GetProperty("price").GetDecimal());

            factory.Clock.UtcNow = new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero);
            using var novemberResponse = await client.GetAsync("/api/public/properties/1");
            Assert.Equal(HttpStatusCode.OK, novemberResponse.StatusCode);
            using var november = JsonDocument.Parse(await novemberResponse.Content.ReadAsStringAsync());
            var novemberRates = november.RootElement.GetProperty("livingCosts").EnumerateArray().ToArray();
            Assert.Equal(new[] { 12, 11 }, novemberRates.Select(rate => rate.GetProperty("serviceId").GetInt32()));
            Assert.Equal(3_300m, novemberRates[0].GetProperty("price").GetDecimal());
            var novemberRate = novemberRates.Single(rate => rate.GetProperty("serviceId").GetInt32() == 11);
            Assert.Equal(150_000m, novemberRate.GetProperty("price").GetDecimal());
            Assert.Equal("2026-11-01", novemberRate.GetProperty("effectiveFrom").GetString());
        }
        finally
        {
            await DropDatabaseAsync(serverConnectionString, databaseName);
        }
    }

    [Phase31BPostgreSqlFact]
    public async Task Feature_off_preserves_public_and_management_gets_on_pre_enrichment_schema()
    {
        var serverConnectionString = GetIsolatedServerConnectionString();
        var databaseName = NewDatabaseName("legacy");
        await CreateDatabaseAsync(serverConnectionString, databaseName);

        try
        {
            var databaseConnectionString = WithDatabase(serverConnectionString, databaseName);
            await MigrateAsync(databaseConnectionString, LegacySchemaTarget);
            var fixture = await SeedLegacyFixtureAsync(databaseConnectionString);

            using var factory = new PublicBrowsingPostgreSqlApiFactory(
                databaseConnectionString,
                propertyDetailEnrichmentEnabled: false,
                new MutableTimeProvider(new DateTimeOffset(2026, 10, 19, 5, 0, 0, TimeSpan.Zero)));
            using var client = factory.CreateClient();

            using var listingResponse = await client.GetAsync("/api/public/properties?pageSize=1");
            Assert.Equal(HttpStatusCode.OK, listingResponse.StatusCode);
            using var listing = JsonDocument.Parse(await listingResponse.Content.ReadAsStringAsync());
            var propertyId = Assert.Single(listing.RootElement.GetProperty("items").EnumerateArray())
                .GetProperty("propertyId").GetInt32();

            factory.SqlCapture.Commands.Clear();
            using var detailResponse = await client.GetAsync($"/api/public/properties/{propertyId}");
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null, detail.RootElement.GetProperty("description").ValueKind);
            Assert.Empty(detail.RootElement.GetProperty("commonAmenities").EnumerateArray());
            Assert.Empty(detail.RootElement.GetProperty("livingCosts").EnumerateArray());

            using var roomPageResponse = await client.GetAsync($"/api/public/properties/{propertyId}/rooms");
            using var publicRoomResponse = await client.GetAsync($"/api/public/rooms/{fixture.RoomId}");
            using var propertyListResponse = await client.GetAsync("/api/properties");
            using var propertyByIdResponse = await client.GetAsync($"/api/properties/{propertyId}");
            using var roomListResponse = await client.GetAsync($"/api/rooms?propertyId={propertyId}");
            using var roomByIdResponse = await client.GetAsync($"/api/rooms/{fixture.RoomId}");
            Assert.Equal(HttpStatusCode.OK, roomPageResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, publicRoomResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, propertyListResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, propertyByIdResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, roomListResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, roomByIdResponse.StatusCode);

            var legacyReadSql = string.Join(Environment.NewLine, factory.SqlCapture.Commands);
            Assert.DoesNotContain("Description", legacyReadSql, StringComparison.Ordinal);
            Assert.DoesNotContain("PropertyAmenities", legacyReadSql, StringComparison.Ordinal);
            Assert.DoesNotContain("PropertyServiceRates", legacyReadSql, StringComparison.Ordinal);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(databaseConnectionString)
                .Options;
            await using var context = new AppDbContext(options);
            var exception = await Record.ExceptionAsync(async () =>
                await new PropertyRepository(context).GetTrackedByIdAsync(propertyId));
            Assert.Equal("42703", FindPostgresException(exception)?.SqlState);
        }
        finally
        {
            await DropDatabaseAsync(serverConnectionString, databaseName);
        }
    }

    private static async Task MigrateAsync(string connectionString, string? targetMigration)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new AppDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    private static async Task SeedMigratedFixtureAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new AppDbContext(options);
        var roleId = await context.Roles.Select(role => role.RoleId).FirstAsync();
        context.Users.Add(new Users
        {
            UserId = 1,
            RoleId = roleId,
            FullName = "Synthetic fixture user",
            Email = "fixture@example.test",
            PasswordHash = "synthetic-test-hash",
            PhoneNumber = "0000000000",
            IsActive = true
        });
        await context.SaveChangesAsync();

        context.Properties.AddRange(
            CreateProperty(1, "Fixture Alpha", "Verified Alpha description"),
            CreateProperty(2, "Fixture Beta", "Verified Beta description"),
            CreateProperty(3, "Fixture Empty", null),
            CreateProperty(4, "Fixture Unavailable", "Not public"));
        context.RoomTypes.AddRange(
            CreateRoomType(1, "Studio", 1_500_000m),
            CreateRoomType(2, "Studio", 2_500_000m),
            CreateRoomType(3, "Studio", 1_200_000m),
            CreateRoomType(4, "Studio", 1_000_000m),
            CreateRoomType(5, "Studio", 3_000_000m),
            CreateRoomType(6, "Studio", 3_500_000m),
            CreateRoomType(7, "Studio", 4_000_000m));
        context.Amenities.AddRange(
            new Amenities { AmenityId = 1, Name = "Common Alpha" },
            new Amenities { AmenityId = 2, Name = "Common Beta" },
            new Amenities { AmenityId = 3, Name = "Room amenity" },
            new Amenities { AmenityId = 4, Name = "Blocked room amenity" });
        context.Services.AddRange(
            new Service { ServiceId = 11, Name = "Internet", Price = 100_000m },
            new Service { ServiceId = 12, Name = "Electricity", Price = 3_500m });
        await context.SaveChangesAsync();

        context.Rooms.AddRange(
            CreateRoom(101, 1, 1),
            CreateRoom(102, 1, 2),
            CreateRoom(103, 1, 3),
            CreateRoom(104, 1, 4),
            CreateRoom(201, 2, 5),
            CreateRoom(301, 3, 6),
            CreateRoom(401, 4, 7));
        await context.SaveChangesAsync();

        context.PropertyAmenities.AddRange(
            new PropertyAmenities { PropertyId = 1, AmenityId = 1 },
            new PropertyAmenities { PropertyId = 2, AmenityId = 2 });
        context.RoomAmenities.AddRange(
            new RoomAmenities { RoomId = 101, AmenityId = 3 },
            new RoomAmenities { RoomId = 102, AmenityId = 3 },
            new RoomAmenities { RoomId = 103, AmenityId = 4 },
            new RoomAmenities { RoomId = 201, AmenityId = 4 });
        context.Bookings.AddRange(
            CreateBooking(1, 103),
            CreateBooking(2, 401));
        context.Contracts.Add(new Contracts
        {
            ContractId = 1,
            RoomId = 104,
            CustomerId = 1,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1),
            MonthlyRent = 1_000_000m,
            DepositAmount = 0m,
            Status = "Active"
        });
        context.RoomTypeServices.Add(new RoomTypeServices { RoomTypeId = 1, ServiceId = 11 });
        context.PropertyServiceRates.AddRange(
            CreateRate(1, 11, 120_000m, "room_month", "fixed_room", new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)),
            CreateRate(1, 11, 150_000m, "room_month", "fixed_room", new DateOnly(2026, 11, 1), null),
            CreateRate(2, 11, 180_000m, "room_month", "fixed_room", new DateOnly(2026, 10, 1), null),
            CreateRate(1, 12, 3_000m, "kwh", "usage", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)),
            CreateRate(1, 12, 3_300m, "kwh", "usage", new DateOnly(2026, 11, 1), null));
        await context.SaveChangesAsync();
    }

    private static async Task<LegacyFixture> SeedLegacyFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var roleCommand = new NpgsqlCommand("SELECT \"RoleId\" FROM \"Roles\" ORDER BY \"RoleId\" LIMIT 1;", connection);
        var roleId = Convert.ToInt32(await roleCommand.ExecuteScalarAsync());
        var userId = await InsertScalarAsync(connection,
            "INSERT INTO \"Users\" (\"RoleId\", \"FullName\", \"Email\", \"PasswordHash\", \"PhoneNumber\", \"IsActive\") VALUES (@roleId, @name, @email, @hash, @phone, TRUE) RETURNING \"UserId\";",
            new NpgsqlParameter("roleId", roleId),
            new NpgsqlParameter("name", "Synthetic fixture user"),
            new NpgsqlParameter("email", "legacy-fixture@example.test"),
            new NpgsqlParameter("hash", "synthetic-test-hash"),
            new NpgsqlParameter("phone", "0000000000"));
        var propertyId = await InsertScalarAsync(connection,
            "INSERT INTO \"Properties\" (\"OwnerId\", \"PropertyName\", \"AddressText\", \"Ward\", \"District\", \"City\", \"Latitude\", \"Longitude\", \"PrimaryImageUrl\") VALUES (@ownerId, @name, @address, @ward, @district, @city, 0, 0, NULL) RETURNING \"PropertyId\";",
            new NpgsqlParameter("ownerId", userId),
            new NpgsqlParameter("name", "Legacy fixture property"),
            new NpgsqlParameter("address", "Synthetic address"),
            new NpgsqlParameter("ward", "Fixture ward"),
            new NpgsqlParameter("district", "Fixture district"),
            new NpgsqlParameter("city", "Fixture city"));
        var roomTypeId = await InsertScalarAsync(connection,
            "INSERT INTO \"RoomTypes\" (\"TypeName\", \"Price\") VALUES (@name, @price) RETURNING \"RoomTypeId\";",
            new NpgsqlParameter("name", "Fixture studio"),
            new NpgsqlParameter("price", 1_500_000m));
        var roomId = await InsertScalarAsync(connection,
            "INSERT INTO \"Rooms\" (\"PropertyId\", \"RoomTypeId\", \"RoomCode\", \"AreaSqm\", \"Status\") VALUES (@propertyId, @roomTypeId, @code, 20, 'Available') RETURNING \"RoomId\";",
            new NpgsqlParameter("propertyId", propertyId),
            new NpgsqlParameter("roomTypeId", roomTypeId),
            new NpgsqlParameter("code", "Fixture-101"));
        return new LegacyFixture(propertyId, roomId);
    }

    private static async Task<int> InsertScalarAsync(
        NpgsqlConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static Property CreateProperty(int id, string name, string? description) => new()
    {
        PropertyId = id,
        OwnerId = 1,
        PropertyName = name,
        Description = description,
        AddressText = "Synthetic address",
        Ward = "Fixture ward",
        District = "Fixture district",
        City = "Fixture city"
    };

    private static RoomTypes CreateRoomType(int id, string name, decimal price) => new()
    {
        RoomTypeId = id,
        TypeName = name,
        Price = price
    };

    private static Rooms CreateRoom(int roomId, int propertyId, int roomTypeId) => new()
    {
        RoomId = roomId,
        PropertyId = propertyId,
        RoomTypeId = roomTypeId,
        RoomCode = $"Fixture-{roomId}",
        AreaSqm = 20m,
        Status = "Available"
    };

    private static Bookings CreateBooking(int bookingId, int roomId) => new()
    {
        BookingId = bookingId,
        RoomId = roomId,
        CustomerId = 1,
        DepositAmount = 0m,
        Status = "Deposited",
        ExpiryDate = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)
    };

    private static PropertyServiceRates CreateRate(
        int propertyId,
        int serviceId,
        decimal price,
        string unitCode,
        string billingBasis,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo) => new()
        {
            PropertyId = propertyId,
            ServiceId = serviceId,
            Price = price,
            UnitCode = unitCode,
            BillingBasis = billingBasis,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo
        };

    private static string GetIsolatedServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configured));
        var builder = new NpgsqlConnectionStringBuilder(configured)
        {
            Pooling = false,
            Timeout = 8,
            CommandTimeout = 60
        };
        Assert.Contains((builder.Host ?? string.Empty).Split(','), host => host is "127.0.0.1" or "localhost" or "::1");
        Assert.Equal(55432, builder.Port);
        Assert.Equal("postgres", builder.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", builder.ApplicationName);
        return builder.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string databaseName) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName }.ConnectionString;

    private static string NewDatabaseName(string suffix) => $"prn232_phase31c_{suffix}_{Guid.NewGuid():N}";

    private static async Task CreateDatabaseAsync(string serverConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string serverConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgresException)
            {
                return postgresException;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    private sealed record LegacyFixture(int PropertyId, int RoomId);

    private sealed class PublicBrowsingPostgreSqlApiFactory : WebApplicationFactory<PublicPropertiesController>
    {
        private readonly string _connectionString;
        private readonly bool _propertyDetailEnrichmentEnabled;

        public PublicBrowsingPostgreSqlApiFactory(
            string connectionString,
            bool propertyDetailEnrichmentEnabled,
            MutableTimeProvider clock)
        {
            _connectionString = connectionString;
            _propertyDetailEnrichmentEnabled = propertyDetailEnrichmentEnabled;
            Clock = clock;
        }

        public MutableTimeProvider Clock
        {
            get;
        }
        public SqlCaptureInterceptor SqlCapture { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["PublicBrowsing:PropertyDetailEnrichmentEnabled"] = _propertyDetailEnrichmentEnabled.ToString()
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddDbContext<AppDbContext>(options => options
                    .UseNpgsql(_connectionString)
                    .AddInterceptors(SqlCapture, new ReadOnlySessionInterceptor()));
            });
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class ReadOnlySessionInterceptor : DbConnectionInterceptor
    {
        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
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
