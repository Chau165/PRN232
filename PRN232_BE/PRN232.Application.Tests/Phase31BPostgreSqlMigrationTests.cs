using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class Phase31BPostgreSqlMigrationTests
{
    [Phase31BPostgreSqlFact]
    public async Task Migrations_preserve_legacy_invoice_lines_and_enforce_property_rate_invariants()
    {
        var serverConnectionString = GetIsolatedServerConnectionString();
        await AssertIsolatedPostgreSqlAsync(serverConnectionString);

        var cleanDatabase = NewDatabaseName("clean");
        await CreateDatabaseAsync(serverConnectionString, cleanDatabase);
        try
        {
            await VerifyCleanUpgradeAndDataInvariantsAsync(
                WithDatabase(serverConnectionString, cleanDatabase));
        }
        finally
        {
            await DropDatabaseAsync(serverConnectionString, cleanDatabase);
        }

        var liveLikeDatabase = NewDatabaseName("livelike");
        await CreateDatabaseAsync(serverConnectionString, liveLikeDatabase);
        try
        {
            await VerifyLiveLikeUpgradeGateAndReviewScriptsAsync(
                WithDatabase(serverConnectionString, liveLikeDatabase));
        }
        finally
        {
            await DropDatabaseAsync(serverConnectionString, liveLikeDatabase);
        }
    }

    private static async Task VerifyCleanUpgradeAndDataInvariantsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        var migrations = context.Database.GetMigrations().ToArray();
        var amenitiesMigration = FindMigration(migrations, "AddPropertyDescriptionAndAmenities");
        var ratesMigration = FindMigration(migrations, "AddPropertyServiceRates");
        var snapshotMigration = FindMigration(migrations, "AddInvoiceDetailBillingSnapshot");
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(ratesMigration);

        var owner = new Users
        {
            RoleId = 2,
            FullName = "Phase 3.1B isolated test",
            Email = $"phase31b-{Guid.NewGuid():N}@invalid.test",
            PasswordHash = "isolated-test-only",
            PhoneNumber = "0000000000",
            IsActive = true
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var firstProperty = CreateProperty(owner.UserId, "Phase 3.1B property A");
        var secondProperty = CreateProperty(owner.UserId, "Phase 3.1B property B");
        context.Properties.AddRange(firstProperty, secondProperty);

        var amenity = new Amenities { Name = "Phase 3.1B isolated amenity" };
        var services = Enumerable.Range(1, 7)
            .Select(index => new Service { Name = $"Isolated service {index}", Price = 0m })
            .ToArray();
        context.Amenities.Add(amenity);
        context.Services.AddRange(services);
        await context.SaveChangesAsync();

        context.PropertyAmenities.AddRange(
            new PropertyAmenities { PropertyId = firstProperty.PropertyId, AmenityId = amenity.AmenityId },
            new PropertyAmenities { PropertyId = secondProperty.PropertyId, AmenityId = amenity.AmenityId });
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.PropertyAmenities.CountAsync(x => x.AmenityId == amenity.AmenityId));
        await AssertSqlStateAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PropertyAmenities" ("PropertyId", "AmenityId")
                VALUES ({firstProperty.PropertyId}, {amenity.AmenityId})
                """),
            "23505");
        await AssertSqlStateAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PropertyAmenities" ("PropertyId", "AmenityId")
                VALUES ({int.MaxValue}, {amenity.AmenityId})
                """),
            "23503");

        var january = new DateOnly(2040, 1, 1);
        var february = new DateOnly(2040, 2, 1);
        var march = new DateOnly(2040, 3, 1);
        var firstElectricRate = CreateRate(firstProperty.PropertyId, services[0].ServiceId, 3500m, "kwh", "usage", january, february);
        var adjacentElectricRate = CreateRate(firstProperty.PropertyId, services[0].ServiceId, 3600m, "kwh", "usage", february, null);
        var secondPropertyElectricRate = CreateRate(secondProperty.PropertyId, services[0].ServiceId, 3200m, "kwh", "usage", january, null);
        var validModes = new[]
        {
            CreateRate(firstProperty.PropertyId, services[1].ServiceId, 0m, "m3", "usage", january, null),
            CreateRate(firstProperty.PropertyId, services[2].ServiceId, 125m, "room_month", "fixed_room", january, null),
            CreateRate(firstProperty.PropertyId, services[3].ServiceId, 125m, "room_month", "fixed_room", january, null),
            CreateRate(firstProperty.PropertyId, services[4].ServiceId, 125m, "person_month", "occupant", january, null),
            CreateRate(firstProperty.PropertyId, services[5].ServiceId, 125m, "vehicle_month", "vehicle", january, null)
        };
        context.PropertyServiceRates.AddRange(new[]
        {
            firstElectricRate,
            adjacentElectricRate,
            secondPropertyElectricRate
        }.Concat(validModes));
        await context.SaveChangesAsync();

        Assert.Equal(3, await context.PropertyServiceRates.CountAsync(x => x.ServiceId == services[0].ServiceId));
        Assert.Equal(3500m, await context.PropertyServiceRates
            .Where(x => x.PropertyId == firstProperty.PropertyId && x.ServiceId == services[0].ServiceId && x.EffectiveFrom == january)
            .Select(x => x.Price)
            .SingleAsync());
        Assert.Equal(3200m, await context.PropertyServiceRates
            .Where(x => x.PropertyId == secondProperty.PropertyId && x.ServiceId == services[0].ServiceId && x.EffectiveFrom == january)
            .Select(x => x.Price)
            .SingleAsync());
        Assert.Equal(0m, validModes[0].Price);

        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, -1m, "kwh", "usage", new DateOnly(2042, 1, 1), null),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "kwh", "fixed_room", new DateOnly(2042, 1, 1), null),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "unknown", "usage", new DateOnly(2042, 1, 1), null),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "kwh", "usage", new DateOnly(2042, 1, 15), null),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "kwh", "usage", new DateOnly(2042, 2, 1), new DateOnly(2042, 2, 2)),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "kwh", "usage", new DateOnly(2042, 3, 1), new DateOnly(2042, 2, 1)),
            "23514");
        await AssertSqlStateAsync(
            () => InsertRateAsync(context, firstProperty.PropertyId, services[0].ServiceId, 1m, "kwh", "usage", march, null),
            "23P01");

        await VerifyConcurrentOverlapIsRejectedAsync(
            connectionString,
            firstProperty.PropertyId,
            services[6].ServiceId);

        var roomType = new RoomTypes { TypeName = "Phase 3.1B isolated room type", Price = 1m };
        context.RoomTypes.Add(roomType);
        await context.SaveChangesAsync();
        var room = new Rooms
        {
            PropertyId = firstProperty.PropertyId,
            RoomTypeId = roomType.RoomTypeId,
            RoomCode = "TEST-31B",
            AreaSqm = 1m,
            Status = "Available"
        };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();
        var contract = new Contracts
        {
            RoomId = room.RoomId,
            CustomerId = owner.UserId,
            StartDate = january,
            EndDate = new DateOnly(2041, 1, 1),
            MonthlyRent = 1m,
            DepositAmount = 0m,
            Status = "Active"
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();
        var invoice = new Invoices
        {
            ContractId = contract.ContractId,
            InvoiceMonth = january,
            DueDate = february,
            Status = "Unpaid"
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        await InsertLegacyInvoiceLinesAsync(connectionString, invoice.InvoiceId);
        var legacyBefore = await ReadLegacyInvoiceLinesAsync(connectionString, invoice.InvoiceId);
        Assert.Equal(25, legacyBefore.Count);

        await migrator.MigrateAsync(snapshotMigration);
        var legacyAfter = await ReadLegacyInvoiceLinesAsync(connectionString, invoice.InvoiceId);
        Assert.Equal(legacyBefore, legacyAfter);
        Assert.Equal(25, await context.InvoiceDetails.CountAsync(x => x.InvoiceId == invoice.InvoiceId));
        Assert.Equal(25, await context.InvoiceDetails.CountAsync(x =>
            x.InvoiceId == invoice.InvoiceId && x.ServiceId == null && x.RateId == null &&
            x.Quantity == null && x.UnitPrice == null && x.UnitCode == null && x.BillingBasis == null));

        var snapshot = new InvoiceDetails
        {
            InvoiceId = invoice.InvoiceId,
            ItemType = "Service",
            Description = "isolated snapshot test",
            Amount = 3948m,
            ServiceId = services[0].ServiceId,
            RateId = secondPropertyElectricRate.RateId,
            Quantity = 1.2345m,
            UnitPrice = 3200m,
            UnitCode = "kwh",
            BillingBasis = "usage"
        };
        context.InvoiceDetails.Add(snapshot);
        await context.SaveChangesAsync();

        secondPropertyElectricRate.EffectiveTo = february;
        context.PropertyServiceRates.Add(CreateRate(
            secondProperty.PropertyId,
            services[0].ServiceId,
            3300m,
            "kwh",
            "usage",
            february,
            null));
        await context.SaveChangesAsync();

        var persistedSnapshot = await context.InvoiceDetails.SingleAsync(x => x.InvoiceDetailId == snapshot.InvoiceDetailId);
        Assert.Equal(services[0].ServiceId, persistedSnapshot.ServiceId);
        Assert.Equal(secondPropertyElectricRate.RateId, persistedSnapshot.RateId);
        Assert.Equal(1.2345m, persistedSnapshot.Quantity);
        Assert.Equal(3200m, persistedSnapshot.UnitPrice);
        Assert.Equal("kwh", persistedSnapshot.UnitCode);
        Assert.Equal("usage", persistedSnapshot.BillingBasis);
        Assert.Equal(3948m, persistedSnapshot.Amount);
        await AssertSqlStateAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"PropertyServiceRates\" WHERE \"RateId\" = {secondPropertyElectricRate.RateId}"),
            "23503");

        await AssertSqlStateAsync(
            () => InsertSnapshotWithMismatchedServiceAsync(context, invoice.InvoiceId, services[1].ServiceId, secondPropertyElectricRate.RateId),
            "23503");

        Assert.True(await IsRowLevelSecurityEnabledAsync(context, "PropertyAmenities"));
        Assert.True(await IsRowLevelSecurityEnabledAsync(context, "PropertyServiceRates"));
        Assert.Equal(0, await ReadPolicyCountAsync(connectionString, "PropertyAmenities"));
        Assert.Equal(0, await ReadPolicyCountAsync(connectionString, "PropertyServiceRates"));
        Assert.True(await IsExtensionInstalledAsync(context, "btree_gist"));
        Assert.Equal(amenitiesMigration, FindMigration(migrations, "AddPropertyDescriptionAndAmenities"));
    }

    private static async Task VerifyLiveLikeUpgradeGateAndReviewScriptsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new AppDbContext(options);
        var migrations = context.Database.GetMigrations().ToArray();
        var maintenanceMigration = FindMigration(migrations, "ReconcileMaintenanceReportsAndLogNullability");
        var imageMigration = FindMigration(migrations, "AddPropertyPrimaryImageUrl");
        var amenitiesMigration = FindMigration(migrations, "AddPropertyDescriptionAndAmenities");
        var ratesMigration = FindMigration(migrations, "AddPropertyServiceRates");
        var snapshotMigration = FindMigration(migrations, "AddInvoiceDetailBillingSnapshot");
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(imageMigration);
        var legacyPropertyId = await SeedLegacyPropertyAsync(connectionString);
        await SimulateAuditedMaintenanceDriftAsync(connectionString, maintenanceMigration);

        var migrateFailure = await Record.ExceptionAsync(() => context.Database.MigrateAsync());
        Assert.NotNull(migrateFailure);
        Assert.Equal("42P07", FindPostgresException(migrateFailure)?.SqlState);

        await ApplyReviewScriptAsync(
            connectionString,
            migrator.GenerateScript(imageMigration, amenitiesMigration));
        await ApplyReviewScriptAsync(
            connectionString,
            migrator.GenerateScript(amenitiesMigration, ratesMigration));
        await ApplyReviewScriptAsync(
            connectionString,
            migrator.GenerateScript(ratesMigration, snapshotMigration));

        Assert.Null(await ReadNullableDescriptionAsync(connectionString, legacyPropertyId));
        Assert.Equal(0, await ReadCountAsync(connectionString, "PropertyServiceRates"));
        Assert.False(await HistoryContainsAsync(connectionString, maintenanceMigration));
        Assert.True(await HistoryContainsAsync(connectionString, amenitiesMigration));
        Assert.True(await HistoryContainsAsync(connectionString, ratesMigration));
        Assert.True(await HistoryContainsAsync(connectionString, snapshotMigration));
        Assert.Equal("CURRENT_TIMESTAMP", await ReadMaintenanceCreatedAtDefaultAsync(connectionString));
        Assert.False(await MaintenanceIndexExistsAsync(connectionString, "IX_MaintenanceReports_ReportedBy"));
        Assert.False(await MaintenanceIndexExistsAsync(connectionString, "IX_MaintenanceReportImages_ReportId"));
    }

    private static Property CreateProperty(int ownerId, string name) => new()
    {
        OwnerId = ownerId,
        PropertyName = name,
        AddressText = "isolated test address",
        Ward = "test ward",
        District = "test district",
        City = "test city",
        Latitude = 0m,
        Longitude = 0m
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

    private static Task<int> InsertRateAsync(
        AppDbContext context,
        int propertyId,
        int serviceId,
        decimal price,
        string unitCode,
        string billingBasis,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo) => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PropertyServiceRates"
                ("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis", "EffectiveFrom", "EffectiveTo")
            VALUES
                ({propertyId}, {serviceId}, {price}, {unitCode}, {billingBasis}, {effectiveFrom}, {effectiveTo})
            """);

    private static async Task VerifyConcurrentOverlapIsRejectedAsync(
        string connectionString,
        int propertyId,
        int serviceId)
    {
        await using var firstConnection = new NpgsqlConnection(connectionString);
        await using var secondConnection = new NpgsqlConnection(connectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = await secondConnection.BeginTransactionAsync();

        await using (var firstInsert = new NpgsqlCommand("""
            INSERT INTO "PropertyServiceRates"
                ("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis", "EffectiveFrom", "EffectiveTo")
            VALUES ($1, $2, 1, 'kwh', 'usage', DATE '2045-01-01', DATE '2045-02-01')
            """, firstConnection, firstTransaction))
        {
            firstInsert.Parameters.AddWithValue(propertyId);
            firstInsert.Parameters.AddWithValue(serviceId);
            await firstInsert.ExecuteNonQueryAsync();
        }

        await using var secondInsert = new NpgsqlCommand("""
            INSERT INTO "PropertyServiceRates"
                ("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis", "EffectiveFrom", "EffectiveTo")
            VALUES ($1, $2, 1, 'kwh', 'usage', DATE '2045-01-01', DATE '2045-03-01')
            """, secondConnection, secondTransaction);
        secondInsert.Parameters.AddWithValue(propertyId);
        secondInsert.Parameters.AddWithValue(serviceId);
        var competingInsert = secondInsert.ExecuteNonQueryAsync();
        await Task.Delay(150);
        await firstTransaction.CommitAsync();

        var failure = await Record.ExceptionAsync(async () => await competingInsert);
        Assert.Equal("23P01", FindPostgresException(failure)?.SqlState);
        await secondTransaction.RollbackAsync();
    }

    private static async Task InsertLegacyInvoiceLinesAsync(string connectionString, int invoiceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        for (var index = 1; index <= 25; index++)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO "InvoiceDetails" ("InvoiceId", "ItemType", "Description", "Amount")
                VALUES (@invoiceId, @itemType, @description, @amount)
                """, connection);
            command.Parameters.AddWithValue("invoiceId", invoiceId);
            command.Parameters.AddWithValue("itemType", $"legacy-type-{index % 5}");
            command.Parameters.AddWithValue("description", $"legacy-line-{index}");
            command.Parameters.AddWithValue("amount", (decimal)index + 0.25m);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<LegacyInvoiceLine>> ReadLegacyInvoiceLinesAsync(string connectionString, int invoiceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT "InvoiceDetailId", "ItemType", "Description", "Amount"
            FROM "InvoiceDetails"
            WHERE "InvoiceId" = @invoiceId
            ORDER BY "InvoiceDetailId"
            """, connection);
        command.Parameters.AddWithValue("invoiceId", invoiceId);
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<LegacyInvoiceLine>();
        while (await reader.ReadAsync())
        {
            lines.Add(new LegacyInvoiceLine(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3)));
        }

        return lines;
    }

    private static async Task InsertSnapshotWithMismatchedServiceAsync(
        AppDbContext context,
        int invoiceId,
        int mismatchedServiceId,
        int rateId) => await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InvoiceDetails"
                ("InvoiceId", "ItemType", "Description", "Amount", "ServiceId", "RateId", "Quantity", "UnitPrice", "UnitCode", "BillingBasis")
            VALUES
                ({invoiceId}, 'Service', 'invalid pair', 1, {mismatchedServiceId}, {rateId}, 1, 1, 'kwh', 'usage')
            """);

    private static async Task<bool> IsRowLevelSecurityEnabledAsync(AppDbContext context, string tableName) =>
        await context.Database.SqlQueryRaw<bool>("""
            SELECT c.relrowsecurity AS "Value"
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = {0}
            """, tableName).SingleAsync();

    private static async Task<bool> IsExtensionInstalledAsync(AppDbContext context, string extensionName) =>
        await context.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = {0}) AS "Value"
            """, extensionName).SingleAsync();

    private static async Task<int> SeedLegacyPropertyAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        int ownerId;
        await using (var userCommand = new NpgsqlCommand("""
            INSERT INTO "Users" ("RoleId", "FullName", "Email", "PasswordHash", "PhoneNumber", "IsActive")
            VALUES (2, 'Phase 3.1B isolated fixture', @email, 'isolated-test-only', '0000000000', TRUE)
            RETURNING "UserId"
            """, connection))
        {
            userCommand.Parameters.AddWithValue("email", $"phase31b-{Guid.NewGuid():N}@invalid.test");
            ownerId = (int)(await userCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Fixture user insert returned no id."));
        }

        await using var propertyCommand = new NpgsqlCommand("""
            INSERT INTO "Properties"
                ("OwnerId", "PropertyName", "AddressText", "Ward", "District", "City", "Latitude", "Longitude")
            VALUES
                (@ownerId, 'Legacy property before phase 3.1B', 'isolated test address', 'test ward', 'test district', 'test city', 0, 0)
            RETURNING "PropertyId"
            """, connection);
        propertyCommand.Parameters.AddWithValue("ownerId", ownerId);
        return (int)(await propertyCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Fixture property insert returned no id."));
    }

    private static async Task SimulateAuditedMaintenanceDriftAsync(string connectionString, string maintenanceMigration)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var drift = new NpgsqlCommand("""
            ALTER TABLE "MaintenanceReports" ALTER COLUMN "CreatedAt" SET DEFAULT CURRENT_TIMESTAMP;
            DROP INDEX IF EXISTS "IX_MaintenanceReports_ReportedBy";
            DROP INDEX IF EXISTS "IX_MaintenanceReportImages_ReportId";
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migrationId;
            """, connection))
        {
            drift.Parameters.AddWithValue("migrationId", maintenanceMigration);
            await drift.ExecuteNonQueryAsync();
        }
    }

    private static async Task ApplyReviewScriptAsync(string connectionString, string script)
    {
        Assert.False(string.IsNullOrWhiteSpace(script));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(script, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> HistoryContainsAsync(string connectionString, string migrationId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @migrationId)",
            connection);
        command.Parameters.AddWithValue("migrationId", migrationId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<int> ReadCountAsync(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM \"{tableName}\"", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ReadNullableDescriptionAsync(string connectionString, int propertyId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"Description\" FROM \"Properties\" WHERE \"PropertyId\" = @propertyId",
            connection);
        command.Parameters.AddWithValue("propertyId", propertyId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : (string)value;
    }

    private static async Task<string> ReadMaintenanceCreatedAtDefaultAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT column_default FROM information_schema.columns
            WHERE table_schema='public' AND table_name='MaintenanceReports' AND column_name='CreatedAt'
            """, connection);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<bool> MaintenanceIndexExistsAsync(string connectionString, string indexName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname=@indexName)",
            connection);
        command.Parameters.AddWithValue("indexName", indexName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<int> ReadPolicyCountAsync(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_policies WHERE schemaname='public' AND tablename=@tableName",
            connection);
        command.Parameters.AddWithValue("tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task AssertIsolatedPostgreSqlAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains((builder.Host ?? string.Empty).Split(','), host => host is "127.0.0.1" or "localhost" or "::1");
        Assert.Equal(55432, builder.Port);
        Assert.Equal("postgres", builder.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", builder.ApplicationName);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT current_database(), current_setting('server_version_num'), current_setting('transaction_read_only')",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("postgres", reader.GetString(0));
        Assert.StartsWith("17", reader.GetString(1), StringComparison.Ordinal);
        Assert.Equal("off", reader.GetString(2));
    }

    private static string GetIsolatedServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Migration tests require the task-created loopback PostgreSQL instance.");
        var builder = new NpgsqlConnectionStringBuilder(configured)
        {
            Pooling = false,
            Timeout = 8,
            CommandTimeout = 60
        };
        return builder.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName };
        return builder.ConnectionString;
    }

    private static string NewDatabaseName(string suffix) => $"prn232_phase31b_{suffix}_{Guid.NewGuid():N}";

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

    private static string FindMigration(IEnumerable<string> migrations, string suffix) =>
        Assert.Single(migrations, migration => migration.EndsWith("_" + suffix, StringComparison.Ordinal));

    private static async Task AssertSqlStateAsync(Func<Task> action, string expectedSqlState)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.NotNull(exception);
        Assert.Equal(expectedSqlState, FindPostgresException(exception!)?.SqlState);
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

    private sealed record LegacyInvoiceLine(int Id, string ItemType, string Description, decimal Amount);
}

public sealed class Phase31BPostgreSqlFactAttribute : FactAttribute
{
    public Phase31BPostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION")))
        {
            Skip = "Set PRN232_MIGRATION_TEST_POSTGRES_CONNECTION to the dedicated loopback PostgreSQL migration-test server.";
        }
    }
}
