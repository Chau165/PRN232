using Microsoft.EntityFrameworkCore;
using Npgsql;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class SupabaseReadOnlyInspectionTests
{
    [Fact]
    public async Task Inspect_configured_supabase_schema_and_migration_history_without_writes()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("aws-0-ap-southeast-1.pooler.supabase.com", builder.Host);
        Assert.Equal("postgres", builder.Database);
        var configuredMigrations = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        await using var modelContext = new AppDbContext(configuredMigrations);
        var sourceMigrations = modelContext.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();

        await using var command = new NpgsqlCommand("""
            SELECT current_database(), inet_server_addr()::text,
                   EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MaintenanceRequests'),
                   EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory')
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("postgres", reader.GetString(0));
        var serverAddress = reader.IsDBNull(1) ? "unknown" : reader.GetString(1);
        var requestsTableExists = reader.GetBoolean(2);
        var historyTableExists = reader.GetBoolean(3);
        await reader.CloseAsync();
        Assert.True(requestsTableExists);
        Assert.True(historyTableExists);

        command.CommandText = """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'MaintenanceRequests' AND column_name = 'AssignmentNote'
            """;
        await using var columnReader = await command.ExecuteReaderAsync();
        var columnState = await columnReader.ReadAsync()
            ? $"{columnReader.GetString(0)}, nullable={columnReader.GetString(1)}, default={(columnReader.IsDBNull(2) ? "NULL" : "set")}"
            : "missing";
        await columnReader.CloseAsync();

        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'MaintenanceRequests' AND indexdef ILIKE '%AssignmentNote%')";
        var noteIndexExists = (bool)(await command.ExecuteScalarAsync())!;
        command.CommandText = "SELECT count(*) FROM public.\"MaintenanceRequests\"";
        var requestCount = (long)(await command.ExecuteScalarAsync())!;
        command.CommandText = "SELECT count(*) FROM public.\"MaintenanceLogs\"";
        var logCount = (long)(await command.ExecuteScalarAsync())!;
        command.CommandText = "SELECT string_agg(\"MigrationId\", ', ' ORDER BY \"MigrationId\") FROM public.\"__EFMigrationsHistory\"";
        var migrations = (string?)await command.ExecuteScalarAsync() ?? "none";
        var appliedMigrations = migrations.Split(", ", StringSplitOptions.RemoveEmptyEntries);
        var sourceDrift = appliedMigrations.Except(sourceMigrations, StringComparer.Ordinal).ToArray();

        var backupDatabase = Environment.GetEnvironmentVariable("PRN232_SUPABASE_BACKUP_VERIFIED");
        var sharedDatabaseApproved = Environment.GetEnvironmentVariable("PRN232_SUPABASE_SCHEMA_CHANGE_APPROVED");
        Assert.True(sourceDrift.Length == 0 && !string.IsNullOrWhiteSpace(backupDatabase) &&
                    !string.IsNullOrWhiteSpace(sharedDatabaseApproved),
            $"No schema change applied. Read-only Supabase inspection: database=postgres; server={serverAddress}; AssignmentNote={columnState}; noteIndex={noteIndexExists}; requests={requestCount}; logs={logCount}; migrations={migrations}; sourceMigrationDrift={string.Join(", ", sourceDrift)}; backupVerified={!string.IsNullOrWhiteSpace(backupDatabase)}; sharedDatabaseChangeApproved={!string.IsNullOrWhiteSpace(sharedDatabaseApproved)}");
        await transaction.RollbackAsync();
    }
}
