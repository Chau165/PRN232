using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AssignmentNoteMigrationTests
{
    [Phase31BPostgreSqlFact]
    public async Task Assignment_note_migration_adds_nullable_text_and_rolls_back_only_its_column()
    {
        var configured = Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION")!;
        var server = new NpgsqlConnectionStringBuilder(configured)
        {
            Pooling = false,
            Timeout = 8,
            CommandTimeout = 60
        };
        Assert.Contains((server.Host ?? string.Empty).Split(','), host => host is "127.0.0.1" or "localhost" or "::1");
        Assert.Equal(55432, server.Port);
        Assert.Equal("postgres", server.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", server.ApplicationName);

        var databaseName = $"prn232_assignmentnote_{Guid.NewGuid():N}";
        var databaseCreated = false;
        await using (var admin = new NpgsqlConnection(server.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
            databaseCreated = true;
        }

        var database = new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options;
            await using var context = new AppDbContext(options);
            var migrations = context.Database.GetMigrations().ToArray();
            var before = Assert.Single(migrations, migration => migration.EndsWith("_AddInvoiceDetailBillingSnapshot", StringComparison.Ordinal));
            var target = Assert.Single(migrations, migration => migration.EndsWith("_AddMaintenanceAssignmentNote", StringComparison.Ordinal));
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(before);

            await using (var connection = new NpgsqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                await using var seed = new NpgsqlCommand("INSERT INTO public.\"MaintenanceRequests\" (\"ContractId\", \"Description\", \"Status\") VALUES (1, 'legacy', 'Pending') RETURNING \"RequestId\"", connection);
                var requestId = (int)(await seed.ExecuteScalarAsync())!;
                await using var log = new NpgsqlCommand("INSERT INTO public.\"MaintenanceLogs\" (\"RequestId\", \"OldStatus\", \"NewStatus\", \"ChangedAt\") VALUES (@id, NULL, 'Pending', CURRENT_TIMESTAMP)", connection);
                log.Parameters.AddWithValue("id", requestId);
                await log.ExecuteNonQueryAsync();
            }

            await migrator.MigrateAsync(target);
            Assert.Equal("text", await ReadColumnTypeAsync(database.ConnectionString));
            Assert.True(await IsColumnNullableAsync(database.ConnectionString));
            Assert.Equal((1L, 1L), await ReadLegacyCountsAsync(database.ConnectionString));
            Assert.Contains("\"AssignmentNote\" text", context.Database.GenerateCreateScript(), StringComparison.Ordinal);

            await migrator.MigrateAsync(before);
            Assert.False(await ColumnExistsAsync(database.ConnectionString));
            Assert.Equal((1L, 1L), await ReadLegacyCountsAsync(database.ConnectionString));
        }
        finally
        {
            if (databaseCreated)
            {
                await using var admin = new NpgsqlConnection(server.ConnectionString);
                await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<string?> ReadColumnTypeAsync(string connectionString) =>
        await ReadScalarAsync<string?>(connectionString, "SELECT data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'MaintenanceRequests' AND column_name = 'AssignmentNote'");

    private static async Task<bool> IsColumnNullableAsync(string connectionString) =>
        await ReadScalarAsync<bool>(connectionString, "SELECT is_nullable = 'YES' FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'MaintenanceRequests' AND column_name = 'AssignmentNote'");

    private static async Task<bool> ColumnExistsAsync(string connectionString) =>
        await ReadScalarAsync<bool>(connectionString, "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'MaintenanceRequests' AND column_name = 'AssignmentNote')");

    private static async Task<(long Requests, long Logs)> ReadLegacyCountsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT (SELECT count(*) FROM public.\"MaintenanceRequests\"), (SELECT count(*) FROM public.\"MaintenanceLogs\")", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<T> ReadScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
