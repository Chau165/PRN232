using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ViewingAssignmentMigrationTests
{
    [Phase31BPostgreSqlFact]
    public async Task Migration_adds_nullable_assignment_and_notification_fields_and_rolls_back_schema_only()
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
        server.ApplicationName = "PRN232-ViewingAssignment-Isolated-Test";

        var databaseName = $"prn232_viewing_assignment_{Guid.NewGuid():N}";
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
            var target = Assert.Single(migrations, migration => migration.EndsWith("_AddViewingAssignmentsAndNotificationLinks", StringComparison.Ordinal));
            var targetIndex = Array.IndexOf(migrations, target);
            Assert.True(targetIndex > 0);
            var before = migrations[targetIndex - 1];
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(before);
            var viewingIds = await SeedLegacyDataAsync(database.ConnectionString);
            var viewingCountBefore = await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"ViewingSchedules\"");
            var notificationCountBefore = await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"Notifications\"");

            await migrator.MigrateAsync(target);

            foreach (var (column, dataType) in new[]
            {
                ("AssignedTo", "integer"),
                ("ManagerNote", "text"),
                ("RespondedBy", "integer"),
                ("RespondedAt", "timestamp with time zone")
            })
            {
                Assert.True(await IsNullableColumnAsync(database.ConnectionString, "ViewingSchedules", column));
                Assert.Equal(dataType, await GetColumnDataTypeAsync(database.ConnectionString, "ViewingSchedules", column));
            }
            foreach (var (column, dataType) in new[]
            {
                ("Body", "text"),
                ("RelatedEntityId", "integer")
            })
            {
                Assert.True(await IsNullableColumnAsync(database.ConnectionString, "Notifications", column));
                Assert.Equal(dataType, await GetColumnDataTypeAsync(database.ConnectionString, "Notifications", column));
            }
            Assert.True(await HasIndexAsync(database.ConnectionString, "IX_ViewingSchedules_AssignedTo"));
            Assert.True(await HasIndexAsync(database.ConnectionString, "IX_ViewingSchedules_RespondedBy"));
            Assert.True(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT count(*) = 2 FROM pg_constraint
                WHERE conrelid = 'public."ViewingSchedules"'::regclass
                  AND contype = 'f' AND confdeltype = 'r'
                  AND conname IN ('FK_ViewingSchedules_Users_AssignedTo', 'FK_ViewingSchedules_Users_RespondedBy')
                """));
            Assert.True(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT count(*) = 1 AND bool_and(contype = 'c')
                FROM pg_constraint
                WHERE conrelid = 'public."ViewingSchedules"'::regclass
                  AND conname = 'CK_ViewingSchedules_ManagerNote_500'
                """));
            Assert.Equal(viewingCountBefore, await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"ViewingSchedules\""));
            Assert.Equal(notificationCountBefore, await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"Notifications\""));
            foreach (var (status, id) in viewingIds)
            {
                Assert.True(await ReadScalarAsync<bool>(database.ConnectionString, """
                    SELECT "Status" = @status AND "AssignedTo" IS NULL AND "ManagerNote" IS NULL
                       AND "RespondedBy" IS NULL AND "RespondedAt" IS NULL
                    FROM public."ViewingSchedules" WHERE "ViewingId" = @id
                    """, ("status", status), ("id", id)));
            }
            Assert.Equal(3L, await ReadScalarAsync<long>(database.ConnectionString, """
                SELECT count(*) FROM public."Notifications"
                WHERE "Body" IS NULL AND "RelatedEntityId" IS NULL
                """));
            Assert.True(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = @id)
                """, ("id", target)));

            var managerNote = string.Concat(Enumerable.Repeat("😀", 500));
            var respondedAt = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
            await ExecuteAsync(database.ConnectionString, """
                UPDATE public."ViewingSchedules"
                SET "AssignedTo" = 7, "ManagerNote" = @note,
                    "RespondedBy" = 9, "RespondedAt" = @respondedAt
                WHERE "ViewingId" = @id
                """, ("note", managerNote), ("respondedAt", respondedAt), ("id", viewingIds["Pending"]));
            Assert.True(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT "Status" = 'Pending' AND "AssignedTo" = 7 AND char_length("ManagerNote") = 500
                   AND "RespondedBy" = 9 AND "RespondedAt" = @respondedAt
                FROM public."ViewingSchedules" WHERE "ViewingId" = @id
                """, ("respondedAt", respondedAt), ("id", viewingIds["Pending"])));

            await ExecuteAsync(database.ConnectionString, """
                UPDATE public."ViewingSchedules" SET "ManagerNote" = NULL WHERE "ViewingId" = @id
                """, ("id", viewingIds["Pending"]));
            Assert.True(await ReadScalarAsync<bool>(database.ConnectionString,
                "SELECT \"ManagerNote\" IS NULL FROM public.\"ViewingSchedules\" WHERE \"ViewingId\" = @id",
                ("id", viewingIds["Pending"])));
            await AssertSqlStateAsync(database.ConnectionString, """
                UPDATE public."ViewingSchedules" SET "AssignedTo" = 999999 WHERE "ViewingId" = @id
                """, "23503", ("id", viewingIds["Pending"]));
            await AssertSqlStateAsync(database.ConnectionString, """
                UPDATE public."ViewingSchedules" SET "RespondedBy" = 999999 WHERE "ViewingId" = @id
                """, "23503", ("id", viewingIds["Pending"]));
            await AssertSqlStateAsync(database.ConnectionString, """
                UPDATE public."ViewingSchedules" SET "ManagerNote" = repeat('x', 501) WHERE "ViewingId" = @id
                """, "23514", ("id", viewingIds["Pending"]));
            Assert.Contains("\"RelatedEntityId\" integer", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
            Assert.Contains("CK_ViewingSchedules_ManagerNote_500", context.Database.GenerateCreateScript(), StringComparison.Ordinal);

            await migrator.MigrateAsync(before);
            Assert.False(await ColumnExistsAsync(database.ConnectionString, "ViewingSchedules", "AssignedTo"));
            Assert.False(await ColumnExistsAsync(database.ConnectionString, "Notifications", "RelatedEntityId"));
            Assert.False(await HasIndexAsync(database.ConnectionString, "IX_ViewingSchedules_AssignedTo"));
            Assert.False(await HasIndexAsync(database.ConnectionString, "IX_ViewingSchedules_RespondedBy"));
            Assert.False(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conrelid = 'public."ViewingSchedules"'::regclass
                  AND conname = 'CK_ViewingSchedules_ManagerNote_500')
                """));
            Assert.False(await ReadScalarAsync<bool>(database.ConnectionString, """
                SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = @id)
                """, ("id", target)));
            Assert.Equal(viewingCountBefore, await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"ViewingSchedules\""));
            Assert.Equal(notificationCountBefore, await ReadScalarAsync<long>(database.ConnectionString,
                "SELECT count(*) FROM public.\"Notifications\""));
            Assert.Equal(1L, await ReadScalarAsync<long>(database.ConnectionString, """
                SELECT count(*) FROM public."ViewingSchedules" WHERE "Status" = 'Approved'
                """));
            Assert.Equal(1L, await ReadScalarAsync<long>(database.ConnectionString, """
                SELECT count(*) FROM public."ViewingSchedules" WHERE "Status" = 'Rejected'
                """));
            Assert.Equal(1L, await ReadScalarAsync<long>(database.ConnectionString, """
                SELECT count(*) FROM public."ViewingSchedules" WHERE "Status" = 'Pending'
                """));
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

    private static Task<bool> IsNullableColumnAsync(string connectionString, string table, string column) =>
        ReadScalarAsync<bool>(connectionString,
            "SELECT is_nullable = 'YES' FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column",
            ("table", table), ("column", column));

    private static Task<string> GetColumnDataTypeAsync(string connectionString, string table, string column) =>
        ReadScalarAsync<string>(connectionString,
            "SELECT data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column",
            ("table", table), ("column", column));

    private static Task<bool> ColumnExistsAsync(string connectionString, string table, string column) =>
        ReadScalarAsync<bool>(connectionString,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)",
            ("table", table), ("column", column));

    private static Task<bool> HasIndexAsync(string connectionString, string indexName) =>
        ReadScalarAsync<bool>(connectionString,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name)",
            ("name", indexName));

    private static async Task<Dictionary<string, int>> SeedLegacyDataAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("""
            INSERT INTO public."Users" ("UserId", "RoleId", "FullName", "Email", "PasswordHash", "PhoneNumber", "IsActive") VALUES
                (7, 2, 'Manager', 'manager@example.test', 'test', '0911000000', TRUE),
                (9, 3, 'Admin', 'admin@example.test', 'test', '0909000000', TRUE),
                (10, 1, 'Customer', 'customer@example.test', 'test', '0910000000', TRUE);
            INSERT INTO public."Properties" ("PropertyId", "OwnerId", "PropertyName", "AddressText", "Ward", "District", "City", "Latitude", "Longitude")
                VALUES (1, 7, 'Sunrise', 'Address', 'Ward', 'District', 'City', 10, 106);
            INSERT INTO public."RoomTypes" ("RoomTypeId", "TypeName", "Price") VALUES (1, 'Studio', 3000000);
            INSERT INTO public."Rooms" ("RoomId", "PropertyId", "RoomTypeId", "RoomCode", "AreaSqm", "Status")
                VALUES (1, 1, 1, 'A101', 20, 'Available');
            INSERT INTO public."Notifications" ("UserId", "Title", "Type", "IsRead") VALUES
                (7, 'Legacy manager notification', 'Viewing', FALSE),
                (9, 'Legacy admin notification', 'Viewing', FALSE),
                (10, 'Legacy customer notification', 'Viewing', FALSE);
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
        await using var insertViewings = new NpgsqlCommand("""
            INSERT INTO public."ViewingSchedules" ("RoomId", "CustomerId", "ScheduledAt", "Status") VALUES
                (1, 10, CURRENT_TIMESTAMP + INTERVAL '2 days', 'Pending'),
                (1, 10, CURRENT_TIMESTAMP + INTERVAL '3 days', 'Approved'),
                (1, 10, CURRENT_TIMESTAMP + INTERVAL '4 days', 'Rejected')
            RETURNING "ViewingId", "Status"
            """, connection);
        await using var reader = await insertViewings.ExecuteReaderAsync();
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(1), reader.GetInt32(0));
        }
        return ids;
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertSqlStateAsync(
        string connectionString,
        string sql,
        string expectedSqlState,
        params (string Name, object? Value)[] parameters)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, sql, parameters));
        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private static async Task<T> ReadScalarAsync<T>(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        AddParameters(command, parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<(string Name, object? Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
