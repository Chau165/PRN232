using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Migrations;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public class ContractMeterHandoverPostgreSqlTests
{
    private static readonly DateOnly Today = new(2026, 12, 15);
    private const string Before = "20261113103807_SecurePaymentTables";

    [Fact]
    public void Migration_only_adds_three_nullable_columns_without_defaults_or_backfill()
    {
        var migration = new AddContractMeterHandoverBaseline();
        var columns = migration.UpOperations.Cast<AddColumnOperation>().ToList();
        Assert.Equal(3, columns.Count);
        Assert.Equal(new[] { "InitialElectricReading", "InitialWaterReading", "MeterHandoverDate" }, columns.Select(column => column.Name));
        Assert.All(columns, column =>
        {
            Assert.Equal("Contracts", column.Table);
            Assert.True(column.IsNullable);
            Assert.Null(column.DefaultValue);
            Assert.Null(column.DefaultValueSql);
        });
        Assert.Equal(new[] { "numeric", "numeric", "date" }, columns.Select(column => column.ColumnType));
        Assert.All(migration.DownOperations, operation => Assert.Equal("Contracts", Assert.IsType<DropColumnOperation>(operation).Table));
        using var db = Context("Host=localhost;Database=unused;Username=test");
        Assert.False(db.Database.HasPendingModelChanges());
        // Look the migration up by name: later migrations must not break this test.
        var target = db.Database.GetMigrations()
            .Single(migration => migration.EndsWith("_AddContractMeterHandoverBaseline", StringComparison.Ordinal));
        var sql = db.GetService<IMigrator>().GenerateScript(Before, target);
        Assert.Equal(3, sql.Split("ALTER TABLE").Length - 1);
        Assert.DoesNotContain("UPDATE \"Contracts\"", sql);
        Assert.Contains("__EFMigrationsHistory", sql);
    }

    [Phase31BPostgreSqlFact]
    public async Task PostgreSQL_migration_preserves_history_and_serializes_baseline_reading_invoice_and_revocation()
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION"))
        {
            Pooling = false, Timeout = 8, CommandTimeout = 60
        };
        Assert.Contains(server.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Equal(55432, server.Port);
        Assert.Equal("postgres", server.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", server.ApplicationName);
        var databaseName = "prn232_meter_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(server.ConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = databaseName }.ConnectionString;
        try
        {
            await using var db = Context(connectionString);
            await db.Database.MigrateAsync();
            await SeedAsync(db);
            var target = db.Database.GetMigrations().Last();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);
            Assert.Equal(25, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var before = await FingerprintAsync(connectionString);
            await migrator.MigrateAsync(target);
            Assert.Equal(before, await FingerprintAsync(connectionString));
            Assert.Equal(26, (await db.Database.GetAppliedMigrationsAsync()).Count());
            db.ChangeTracker.Clear();
            Assert.All(await db.Contracts.ToListAsync(), contract =>
            {
                Assert.Null(contract.InitialElectricReading);
                Assert.Null(contract.InitialWaterReading);
                Assert.Null(contract.MeterHandoverDate);
            });
            var baseline = await SaveAsync(connectionString, 1, 1200m);
            Assert.Equal(ContractMutationStatus.Updated, baseline.Status);
            Assert.Equal(1, await db.UtilityReadings.CountAsync());
            Assert.Equal(ContractMutationStatus.Conflict, (await SaveAsync(connectionString, 2, 1200m)).Status);

            // Both managers start from the same observed baseline; exactly one update can win.
            var updates = await Task.WhenAll(
                SaveAsync(connectionString, 1, 1201m, 1200m, 7),
                SaveAsync(connectionString, 1, 1202m, 1200m, 8));
            Assert.Single(updates, result => result.Status == ContractMutationStatus.Updated);
            Assert.Single(updates, result => result.Status == ContractMutationStatus.Conflict);
            db.ChangeTracker.Clear();
            var current = await db.Contracts.AsNoTracking().SingleAsync(contract => contract.ContractId == 1);
            var initialElectric = current.InitialElectricReading!.Value;

            // Force an old reading request to wait behind an uncommitted baseline edit.
            await using (var writer = Context(connectionString))
            {
                await using var transaction = await writer.Database.BeginTransactionAsync();
                await writer.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = 1 FOR UPDATE");
                var waiting = AddReadingAsync(connectionString, 1, new DateOnly(2026, 11, 1), initialElectric, 45m);
                Assert.False(waiting.IsCompleted);
                await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Contracts\" SET \"InitialElectricReading\" = {initialElectric + 1m} WHERE \"ContractId\" = 1");
                await transaction.CommitAsync();
                Assert.False(await waiting);
                initialElectric += 1m;
            }

            var readingWrites = await Task.WhenAll(
                AddReadingAsync(connectionString, 1, new DateOnly(2026, 11, 1), initialElectric, 45m),
                AddReadingAsync(connectionString, 1, new DateOnly(2026, 11, 1), initialElectric, 45m));
            Assert.Single(readingWrites, success => success);
            Assert.Equal(ContractMutationStatus.Conflict, (await SaveAsync(connectionString, 1, 1400m, initialElectric)).Status);
            Assert.True(await AddReadingAsync(connectionString, 1, new DateOnly(2026, 12, 1), initialElectric + 48m, 49m));
            Assert.False(await AddReadingAsync(connectionString, 1, new DateOnly(2027, 2, 1), initialElectric + 96m, 53m));

            // Invoice-only legacy contract also cannot acquire a fictitious handover.
            Assert.Equal(ContractMutationStatus.Conflict, (await SaveAsync(connectionString, 3, 1200m)).Status);
            // Active state and assignment are re-read after waiting for the shared contract lock.
            await AssertWaitingMutationRejectedAsync(connectionString, "UPDATE \"Contracts\" SET \"Status\" = 'Terminated' WHERE \"ContractId\" = 4", ContractMutationStatus.Conflict);
            await db.Database.ExecuteSqlRawAsync("UPDATE \"Contracts\" SET \"Status\" = 'Active' WHERE \"ContractId\" = 4");
            await AssertWaitingMutationRejectedAsync(connectionString, "DELETE FROM \"PropertyManagerAssignments\" WHERE \"PropertyId\" = 1 AND \"ManagerId\" = 7", ContractMutationStatus.Forbidden);
            Assert.Equal(before, await FingerprintAsync(connectionString));
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP DATABASE \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task AssertWaitingMutationRejectedAsync(string connectionString, string mutation, ContractMutationStatus status)
    {
        await using var writer = Context(connectionString);
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Contracts\" WHERE \"ContractId\" = 4 FOR UPDATE");
        var waiting = SaveAsync(connectionString, 4, 1200m);
        Assert.False(waiting.IsCompleted);
        await writer.Database.ExecuteSqlRawAsync(mutation);
        await transaction.CommitAsync();
        Assert.Equal(status, (await waiting).Status);
    }

    private static async Task<ContractMutationResult> SaveAsync(string connectionString, int id, decimal electric, decimal? expected = null, int manager = 7)
    {
        await using var db = Context(connectionString);
        return await new ContractRepository(db).SaveMeterHandoverAsync(id, manager, new DateOnly(2026, 11, 13), electric, 45m,
            expected.HasValue ? new DateOnly(2026, 11, 13) : null, expected, expected.HasValue ? 45m : null, Today, CancellationToken.None);
    }

    private static async Task<bool> AddReadingAsync(string connectionString, int id, DateOnly month, decimal electricStart, decimal waterStart)
    {
        await using var db = Context(connectionString);
        return await new UtilityReadingRepository(db, new FixedTimeProvider()).AddIfLatestAsync(new UtilityReadings
        {
            ContractId = id, ReadingMonth = month, ElectricStart = electricStart, ElectricEnd = electricStart + 48m,
            WaterStart = waterStart, WaterEnd = waterStart + 4m
        }, 7);
    }

    private static async Task<string> FingerprintAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // Ignore new columns; compare actual historical business values before/after the migration.
        await using var command = new NpgsqlCommand("""
            SELECT jsonb_build_object(
                'contracts', (SELECT jsonb_agg(to_jsonb(c) - 'InitialElectricReading' - 'InitialWaterReading' - 'MeterHandoverDate' ORDER BY "ContractId") FROM "Contracts" c),
                'invoices', (SELECT jsonb_agg(to_jsonb(i) ORDER BY "InvoiceId") FROM "Invoices" i),
                'details', (SELECT jsonb_agg(to_jsonb(d) ORDER BY "InvoiceDetailId") FROM "InvoiceDetails" d),
                'payments', (SELECT jsonb_agg(to_jsonb(p) ORDER BY "PaymentId") FROM "Payments" p))::text
            """, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var managerRole = await db.Roles.SingleAsync(role => role.RoleName == RoleNames.Manager);
        var customerRole = await db.Roles.SingleAsync(role => role.RoleName == RoleNames.Customer);
        foreach (var id in new[] { 7, 8, 10 })
        {
            db.Users.Add(new Users { UserId = id, RoleId = id == 10 ? customerRole.RoleId : managerRole.RoleId,
                FullName = $"Fixture {id}", Email = $"{id}@example.test", PasswordHash = "fixture", IsActive = true });
        }
        db.Properties.Add(new Property { PropertyId = 1, OwnerId = 8, PropertyName = "Fixture", AddressText = "Fixture" });
        db.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Fixture", Price = 3_000_000m });
        for (var id = 1; id <= 4; id++)
        {
            db.Rooms.Add(new Rooms { RoomId = id, PropertyId = 1, RoomTypeId = 1, RoomCode = $"A{id}", Status = RoomStatuses.Occupied });
            db.Contracts.Add(new Contracts { ContractId = id, RoomId = id, CustomerId = 10,
                StartDate = new DateOnly(2026, 11, 12), EndDate = new DateOnly(2027, 11, 12), MonthlyRent = 3_000_000m, Status = ContractStatuses.Active });
        }
        db.PropertyManagerAssignments.AddRange(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
        db.UtilityReadings.Add(new UtilityReadings { ContractId = 2, ReadingMonth = new DateOnly(2026, 11, 1), ElectricStart = 9979m, ElectricEnd = 10000m, WaterStart = 123m, WaterEnd = 155m });
        var invoice = new Invoices { ContractId = 3, InvoiceMonth = new DateOnly(2026, 11, 1), DueDate = Today, Status = InvoiceStatuses.Unpaid };
        invoice.InvoiceDetails.Add(new InvoiceDetails { ItemType = InvoiceItemTypes.Rent, Description = "Historical rent", Amount = 3_000_000m });
        db.Invoices.Add(invoice);
        db.Payments.Add(new Payments { UserId = 10, ContractId = 3, Amount = 100m, PaymentMethod = "Cash", Status = PaymentStatuses.Success });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static AppDbContext Context(string connectionString) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 12, 15, 3, 0, 0, TimeSpan.Zero);
    }
}
