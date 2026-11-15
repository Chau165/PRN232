using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Migrations;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild tables (handover, evidence, settlement, audit log, co-occupants, transfers) hold personal and money data
// and are used only by the .NET API: RLS on with no policy, and no access for Supabase API roles.
public class RebuildTableSecurityMigrationTests
{
    private static readonly string[] Tables =
    [
        "HandoverRecords", "HandoverItems", "ContractEvidenceFiles", "DepositSettlements",
        "AuditLogs", "ContractOccupants", "ContractTransfers"
    ];

    [Fact]
    public void Script_secures_every_rebuild_table_and_never_reopens_on_rollback()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=test;Password=test").Options);
        const string before = "20261115084651_AddContractTransfers";
        const string target = "20261115102725_SecureRebuildTables";
        Assert.Contains(target, db.Database.GetMigrations());
        var sql = db.GetService<IMigrator>().GenerateScript(before, target);
        foreach (var table in Tables)
        {
            Assert.Contains($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY", sql);
            Assert.Contains($"REVOKE ALL PRIVILEGES ON TABLE public.\"{table}\" FROM PUBLIC", sql);
        }
        Assert.Contains("'anon', 'authenticated', 'service_role'", sql);
        Assert.DoesNotContain("CREATE POLICY", sql);
        Assert.Empty(new SecureRebuildTables().DownOperations);
        Assert.DoesNotContain("DISABLE ROW LEVEL SECURITY", db.GetService<IMigrator>().GenerateScript(target, before));
    }

    // Every table created by a migration must end up with RLS on, so a future table cannot slip through again.
    [Fact]
    public void Every_table_created_since_the_rebuild_is_secured()
    {
        var created = typeof(AppDbContext).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Migration)) && type.Name != nameof(SecureRebuildTables) &&
                type.GetCustomAttributes(typeof(MigrationAttribute), false).Cast<MigrationAttribute>()
                    .Any(attribute => string.CompareOrdinal(attribute.Id, "20261115") >= 0))
            .SelectMany(type => ((Migration)Activator.CreateInstance(type)!).UpOperations.OfType<CreateTableOperation>())
            .Select(operation => operation.Name)
            .ToList();

        Assert.NotEmpty(created);
        Assert.All(created, table => Assert.Contains(table, Tables));
    }

    [Phase31BPostgreSqlFact]
    public async Task PostgreSQL_blocks_api_roles_and_keeps_rows_with_or_without_supabase_roles()
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRN232_MIGRATION_TEST_POSTGRES_CONNECTION"))
            { Pooling = false, Timeout = 5, CommandTimeout = 30 };
        Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" });
        Assert.Equal(55432, server.Port);
        var name = "prn232_rebuild_security_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(server.ConnectionString);
        await admin.OpenAsync();
        async Task Admin(string sql) { await using var c = new NpgsqlCommand(sql, admin); await c.ExecuteNonQueryAsync(); }
        await Admin($"CREATE DATABASE \"{name}\"");
        try
        {
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = name }.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            async Task Execute(string sql) { await using var c = new NpgsqlCommand(sql, connection, transaction); await c.ExecuteNonQueryAsync(); }
            async Task<T> Scalar<T>(string sql) { await using var c = new NpgsqlCommand(sql, connection, transaction); return (T)(await c.ExecuteScalarAsync())!; }
            foreach (var table in Tables)
                await Execute($"CREATE TABLE public.\"{table}\" (id integer PRIMARY KEY, secret text); INSERT INTO public.\"{table}\" VALUES (1, 'fixture'); GRANT ALL ON public.\"{table}\" TO PUBLIC;");
            var operations = new SecureRebuildTables().UpOperations.Cast<SqlOperation>().ToList();
            // Chạy được trên PostgreSQL thường (chưa có role API), rồi chạy lại khi đã có role và quyền.
            foreach (var op in operations) await Execute(op.Sql);
            foreach (var role in new[] { "anon", "authenticated", "service_role" })
            {
                if (!await Scalar<bool>($"SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='{role}')")) await Execute($"CREATE ROLE {role} NOLOGIN");
                foreach (var table in Tables) await Execute($"GRANT ALL ON public.\"{table}\" TO {role}");
            }
            foreach (var op in operations) await Execute(op.Sql);
            foreach (var table in Tables)
            {
                Assert.Equal(1L, await Scalar<long>($"SELECT count(*) FROM public.\"{table}\""));
                Assert.True(await Scalar<bool>($"SELECT relrowsecurity FROM pg_class WHERE oid='public.\"{table}\"'::regclass"));
                foreach (var role in new[] { "anon", "authenticated" })
                {
                    await transaction.SaveAsync("denied");
                    await Execute($"SET LOCAL ROLE {role}");
                    var error = await Assert.ThrowsAsync<PostgresException>(() => Execute($"SELECT * FROM public.\"{table}\""));
                    Assert.Equal("42501", error.SqlState);
                    await transaction.RollbackAsync("denied");
                }
            }
            await transaction.RollbackAsync();
        }
        finally { await Admin($"DROP DATABASE \"{name}\" WITH (FORCE)"); }
    }
}
