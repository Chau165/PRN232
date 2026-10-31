using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PRN232.Api.Controllers;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace PRN232.Application.Tests;

public sealed class PublicPropertyListingPostgreSqlTests
{
    private readonly ITestOutputHelper _output;

    public PublicPropertyListingPostgreSqlTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [PostgreSqlFact]
    public async Task Listing_query_executes_on_postgresql_with_a_read_only_session()
    {
        await using var connection = await OpenReadOnlyConnectionAsync();

        var sqlCapture = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(sqlCapture)
            .Options;
        await using var context = new AppDbContext(options);
        var repository = new PublicPropertyListingRepository(context);
        var nowUtc = DateTimeOffset.UtcNow;

        var page = await repository.GetPageAsync(
            new PublicPropertySearchCriteria(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                "price_asc",
                1,
                2,
                0),
            nowUtc,
            CancellationToken.None);

        Assert.InRange(page.Items.Count, 0, 2);
        Assert.True(page.TotalCount >= page.Items.Count);
        Assert.All(page.Items, item =>
        {
            Assert.True(item.AvailableRoomCount > 0);
            Assert.NotNull(item.StartingPrice);
            Assert.Null(item.PrimaryImageUrl);
        });
        Assert.Equal(3, sqlCapture.Commands.Count);
        var generatedSql = string.Join(Environment.NewLine, sqlCapture.Commands);
        Assert.Contains("NOT EXISTS", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIN(", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RoomAmenities", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("RoomImages", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerId", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnerId", generatedSql, StringComparison.Ordinal);

        var filteredPage = await repository.GetPageAsync(
            new PublicPropertySearchCriteria(
                "phase2b-read-only-no-match-20261019%_",
                "phase2b-no-match",
                null,
                null,
                0m,
                100_000_000m,
                "phase2b-no-room-type",
                [1],
                "price_desc",
                1,
                1,
                0),
            nowUtc,
            CancellationToken.None);

        Assert.InRange(filteredPage.Items.Count, 0, 1);
        Assert.True(filteredPage.TotalCount >= filteredPage.Items.Count);
        Assert.Equal(6, sqlCapture.Commands.Count);
    }

    [PostgreSqlFact]
    public async Task Property_detail_and_room_queries_execute_on_postgresql_read_only_with_bounded_selects()
    {
        await using var connection = await OpenReadOnlyConnectionAsync();
        var sqlCapture = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(sqlCapture)
            .Options;
        await using var context = new AppDbContext(options);
        var repository = new PublicPropertyListingRepository(context);
        var nowUtc = DateTimeOffset.UtcNow;
        var listing = await repository.GetPageAsync(
            new PublicPropertySearchCriteria(null, null, null, null, null, null, null, [], "propertyid", 1, 1, 0),
            nowUtc,
            CancellationToken.None);
        Assert.NotEmpty(listing.Items);
        var propertyId = listing.Items[0].PropertyId;

        var detail = await repository.GetDetailAsync(propertyId, nowUtc, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.True(detail.AvailableRoomCount > 0);
        Assert.NotNull(detail.StartingPrice);
        var detailJson = JsonSerializer.SerializeToElement(detail);
        Assert.True(detailJson.TryGetProperty("RoomAmenities", out var roomAmenities));
        Assert.Equal(JsonValueKind.Array, roomAmenities.ValueKind);
        Assert.All(roomAmenities.EnumerateArray(), amenity =>
        {
            Assert.InRange(amenity.GetProperty("RentableRoomCount").GetInt32(), 1, detail.AvailableRoomCount);
        });

        var rooms = await repository.GetRoomsAsync(
            propertyId,
            new PublicPropertySearchCriteria(null, null, null, null, null, null, null, [], "roomid", 1, 2, 0),
            nowUtc,
            CancellationToken.None);
        Assert.NotNull(rooms);
        Assert.InRange(rooms.Items.Count, 0, 2);
        Assert.True(rooms.TotalCount >= rooms.Items.Count);
        Assert.Null(await repository.GetDetailAsync(int.MaxValue, nowUtc, CancellationToken.None));
        Assert.Null(await repository.GetRoomsAsync(
            int.MaxValue,
            new PublicPropertySearchCriteria(null, null, null, null, null, null, null, [], "roomid", 1, 2, 0),
            nowUtc,
            CancellationToken.None));
        Assert.Equal(11, sqlCapture.Commands.Count);

        var generatedSql = string.Join(Environment.NewLine, sqlCapture.Commands);
        Assert.Contains("NOT EXISTS", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIN(", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RoomAmenities", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("RoomImages", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnerId", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerId", generatedSql, StringComparison.Ordinal);

        await using var imageProfileCommand = new NpgsqlCommand(
            """
            WITH image_hosts AS (
                SELECT "ImageUrl",
                       lower(substring(btrim("ImageUrl") from '^https?://([^/?#]+)')) AS host
                FROM public."RoomImages"
            )
            SELECT COUNT(*) AS total,
                   COUNT(*) FILTER (WHERE lower(btrim("ImageUrl")) LIKE 'https://%') AS https_urls,
                   COUNT(*) FILTER (WHERE lower(btrim("ImageUrl")) ~ '^[a-z][a-z0-9+.-]*://') AS absolute_scheme_urls,
                   COUNT(DISTINCT host) AS distinct_hosts,
                   COUNT(*) FILTER (WHERE host LIKE '%.supabase.co') AS supabase_host_rows,
                   COUNT(*) FILTER (WHERE host ~ '(^localhost(:|$)|^127\.|^10\.|^192\.168\.|^172\.(1[6-9]|2[0-9]|3[01])\.)') AS local_or_private_host_rows,
                   COUNT(*) FILTER (WHERE lower(btrim("ImageUrl")) LIKE '%/storage/v1/object/public/%') AS supabase_public_path,
                   COUNT(*) FILTER (WHERE lower(btrim("ImageUrl")) LIKE '%/storage/v1/object/sign/%') AS supabase_signed_path,
                   COUNT(*) FILTER (WHERE position('?' in "ImageUrl") > 0) AS query_string_urls,
                   COUNT(*) FILTER (WHERE lower("ImageUrl") ~ '[?&](token|signature|sig|expires|x-amz-[^=]+)=') AS signed_query_like
            FROM image_hosts
            """,
            connection);
        await using var imageProfile = await imageProfileCommand.ExecuteReaderAsync();
        Assert.True(await imageProfile.ReadAsync());
        var totalImages = imageProfile.GetInt64(0);
        var httpsUrls = imageProfile.GetInt64(1);
        var absoluteSchemeUrls = imageProfile.GetInt64(2);
        var distinctHosts = imageProfile.GetInt64(3);
        var supabaseHostRows = imageProfile.GetInt64(4);
        var localOrPrivateHostRows = imageProfile.GetInt64(5);
        var publicStoragePaths = imageProfile.GetInt64(6);
        var signedStoragePaths = imageProfile.GetInt64(7);
        var queryStringUrls = imageProfile.GetInt64(8);
        var signedLikeQueries = imageProfile.GetInt64(9);
        _output.WriteLine(
            $"RoomImages URL profile (counts only, no raw hosts or paths): total={totalImages}, https={httpsUrls}, absoluteSchemes={absoluteSchemeUrls}, distinctHosts={distinctHosts}, supabaseHostRows={supabaseHostRows}, localOrPrivateHostRows={localOrPrivateHostRows}, publicStoragePaths={publicStoragePaths}, signedStoragePaths={signedStoragePaths}, queryStrings={queryStringUrls}, signedLikeQueries={signedLikeQueries}.");
        Assert.InRange(httpsUrls, 0, totalImages);
        Assert.InRange(absoluteSchemeUrls, 0, totalImages);
        Assert.InRange(distinctHosts, 0, totalImages);
        Assert.InRange(supabaseHostRows, 0, totalImages);
        Assert.InRange(localOrPrivateHostRows, 0, totalImages);
        Assert.InRange(publicStoragePaths, 0, totalImages);
        Assert.InRange(signedStoragePaths, 0, totalImages);
        Assert.InRange(queryStringUrls, 0, totalImages);
        Assert.InRange(signedLikeQueries, 0, totalImages);
    }

    [PostgreSqlFact]
    public async Task Room_detail_executes_on_postgresql_read_only_with_bounded_selects()
    {
        await using var connection = await OpenReadOnlyConnectionAsync();
        var sqlCapture = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(sqlCapture)
            .Options;
        await using var context = new AppDbContext(options);
        var repository = new PublicPropertyListingRepository(context);
        var asOfUtc = DateTimeOffset.UtcNow;
        var eligibleRoomId = await RoomAvailabilityQueries.GetRentableRooms(context.Rooms, asOfUtc)
            .OrderBy(room => room.RoomId)
            .Select(room => room.RoomId)
            .FirstOrDefaultAsync();
        Assert.NotEqual(0, eligibleRoomId);

        sqlCapture.Commands.Clear();
        var detail = await repository.GetRoomDetailAsync(eligibleRoomId, asOfUtc, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(eligibleRoomId, detail.RoomId);
        Assert.NotNull(detail.Property);
        Assert.Empty(detail.Images);
        Assert.Empty(detail.Equipments);
        Assert.Equal(3, sqlCapture.Commands.Count);

        var generatedSql = string.Join(Environment.NewLine, sqlCapture.Commands);
        Assert.Contains("NOT EXISTS", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RoomAmenities", generatedSql, StringComparison.Ordinal);
        Assert.Contains("RoomTypeServices", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("RoomImages", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("Equipments", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnerId", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerId", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("Supplier", generatedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("WarrantyExpiry", generatedSql, StringComparison.Ordinal);
        _output.WriteLine(
            $"Room detail Npgsql SQL (structure only): selects={sqlCapture.Commands.Count}, rentabilityPredicate={generatedSql.Contains("NOT EXISTS", StringComparison.OrdinalIgnoreCase)}, amenityJoin={generatedSql.Contains("RoomAmenities", StringComparison.Ordinal)}, serviceJoin={generatedSql.Contains("RoomTypeServices", StringComparison.Ordinal)}, imageAndEquipmentTablesQueried=false.");

        var blockedRoomId = await context.Rooms
            .AsNoTracking()
            .Where(room => room.Status != "Available")
            .OrderBy(room => room.RoomId)
            .Select(room => room.RoomId)
            .FirstOrDefaultAsync();
        Assert.NotEqual(0, blockedRoomId);
        sqlCapture.Commands.Clear();
        Assert.Null(await repository.GetRoomDetailAsync(blockedRoomId, asOfUtc, CancellationToken.None));
        Assert.Single(sqlCapture.Commands);

        sqlCapture.Commands.Clear();
        Assert.Null(await repository.GetRoomDetailAsync(int.MaxValue, asOfUtc, CancellationToken.None));
        Assert.Single(sqlCapture.Commands);
        _output.WriteLine("Room detail read-only results: eligible=present (3 SELECTs), blocked=hidden (1 SELECT), unknown=hidden (1 SELECT).");
    }

    [PostgreSqlFact]
    public async Task Four_public_browsing_routes_work_over_read_only_postgresql_without_authentication()
    {
        var connectionString = CreateReadOnlyConnectionString();
        using var factory = new PublicBrowsingPostgreSqlApiFactory(connectionString);
        using var client = factory.CreateClient();
        Assert.Null(client.DefaultRequestHeaders.Authorization);

        await AssertDatabaseSessionReadOnlyAsync(factory.Services);
        await AssertPublicBrowsingRoutesAsync(client);
        await AssertManagementWritesRequireAuthenticationAsync(client);
    }

    private static async Task AssertDatabaseSessionReadOnlyAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SHOW default_transaction_read_only";
            Assert.Equal("on", await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task AssertPublicBrowsingRoutesAsync(HttpClient client)
    {
        using var listingResponse = await client.GetAsync("/api/public/properties?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listingResponse.StatusCode);
        using var listing = JsonDocument.Parse(await listingResponse.Content.ReadAsStringAsync());
        var propertyCard = Assert.Single(listing.RootElement.GetProperty("items").EnumerateArray());
        var propertyId = propertyCard.GetProperty("propertyId").GetInt32();
        Assert.Equal(JsonValueKind.Null, propertyCard.GetProperty("primaryImageUrl").ValueKind);

        using var propertyDetailResponse = await client.GetAsync($"/api/public/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.OK, propertyDetailResponse.StatusCode);
        using var propertyDetail = JsonDocument.Parse(await propertyDetailResponse.Content.ReadAsStringAsync());
        Assert.Equal(propertyId, propertyDetail.RootElement.GetProperty("propertyId").GetInt32());
        Assert.Equal(JsonValueKind.Null, propertyDetail.RootElement.GetProperty("primaryImageUrl").ValueKind);
        Assert.False(propertyDetail.RootElement.TryGetProperty("ownerId", out _));

        using var roomPageResponse = await client.GetAsync($"/api/public/properties/{propertyId}/rooms?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, roomPageResponse.StatusCode);
        using var roomPage = JsonDocument.Parse(await roomPageResponse.Content.ReadAsStringAsync());
        var roomCard = Assert.Single(roomPage.RootElement.GetProperty("items").EnumerateArray());
        var roomId = roomCard.GetProperty("roomId").GetInt32();
        Assert.Equal(JsonValueKind.Null, roomCard.GetProperty("primaryImageUrl").ValueKind);

        using var roomDetailResponse = await client.GetAsync($"/api/public/rooms/{roomId}");
        Assert.Equal(HttpStatusCode.OK, roomDetailResponse.StatusCode);
        using var roomDetail = JsonDocument.Parse(await roomDetailResponse.Content.ReadAsStringAsync());
        Assert.Equal(roomId, roomDetail.RootElement.GetProperty("roomId").GetInt32());
        Assert.Empty(roomDetail.RootElement.GetProperty("images").EnumerateArray());
        Assert.Empty(roomDetail.RootElement.GetProperty("equipments").EnumerateArray());
        Assert.False(roomDetail.RootElement.TryGetProperty("status", out _));
        Assert.False(roomDetail.RootElement.TryGetProperty("ownerId", out _));
    }

    private static async Task AssertManagementWritesRequireAuthenticationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/rooms",
            new
            {
                propertyId = 1,
                roomTypeId = 1,
                roomCode = "not-written",
                areaSqm = 1m
            });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateReadOnlyConnectionString()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("PRN232_TEST_POSTGRES_CONNECTION")!;
        var connectionBuilder = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Pooling = false,
            Timeout = 8,
            CommandTimeout = 20
        };
        return connectionBuilder.ConnectionString;
    }

    private sealed class PublicBrowsingPostgreSqlApiFactory : WebApplicationFactory<PublicPropertiesController>
    {
        private readonly string _connectionString;

        public PublicBrowsingPostgreSqlApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options
                        .UseNpgsql(_connectionString)
                        .AddInterceptors(new ReadOnlySessionInterceptor()));
            });
        }
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

    private static async Task<NpgsqlConnection> OpenReadOnlyConnectionAsync()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("PRN232_TEST_POSTGRES_CONNECTION")!;
        var connectionBuilder = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Pooling = false,
            Timeout = 8,
            CommandTimeout = 20
        };
        var connection = new NpgsqlConnection(connectionBuilder.ConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand(
            "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY",
            connection))
        {
            await command.ExecuteNonQueryAsync();
        }
        await using (var command = new NpgsqlCommand("SHOW default_transaction_read_only", connection))
        {
            Assert.Equal("on", await command.ExecuteScalarAsync());
        }

        return connection;
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

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRN232_TEST_POSTGRES_CONNECTION")))
        {
            Skip = "Set PRN232_TEST_POSTGRES_CONNECTION to an isolated PostgreSQL test database, or use the approved read-only verification path.";
        }
    }
}
