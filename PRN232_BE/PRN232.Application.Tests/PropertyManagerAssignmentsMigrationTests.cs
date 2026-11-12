using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyManagerAssignmentsMigrationTests
{
    [Fact]
    public void Assignment_migration_changes_only_the_assignment_table_and_its_security()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var context = new AppDbContext(options);
        var migrations = context.Database.GetMigrations().ToArray();
        var id = Assert.Single(migrations, item => item.EndsWith(
            "_AddPropertyManagerAssignments", StringComparison.Ordinal));
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[id], context.Database.ProviderName!);
        var operations = migration.UpOperations;

        var table = Assert.Single(operations.OfType<CreateTableOperation>());
        Assert.Equal("PropertyManagerAssignments", table.Name);
        Assert.Equal(new[] { "PropertyId", "ManagerId" }, table.Columns.Select(column => column.Name));
        var primaryKey = Assert.IsType<AddPrimaryKeyOperation>(table.PrimaryKey);
        Assert.Equal(new[] { "PropertyId", "ManagerId" }, primaryKey.Columns);
        var foreignKeys = table.ForeignKeys
            .OrderBy(key => key.Name)
            .ToArray();
        Assert.Equal(2, foreignKeys.Length);
        Assert.Equal(new[]
        {
            ("FK_PropertyManagerAssignments_Properties_PropertyId", "Properties", "PropertyId", ReferentialAction.Restrict),
            ("FK_PropertyManagerAssignments_Users_ManagerId", "Users", "ManagerId", ReferentialAction.Restrict)
        }, foreignKeys.Select(key => (key.Name, key.PrincipalTable, key.Columns.Single(), key.OnDelete)));

        var index = Assert.Single(operations.OfType<CreateIndexOperation>(), item => item.Table == "PropertyManagerAssignments");
        Assert.Equal(("PropertyManagerAssignments", "IX_PropertyManagerAssignments_ManagerId", "ManagerId"),
            (index.Table, index.Name, Assert.Single(index.Columns)));
        Assert.DoesNotContain(operations.OfType<CreateIndexOperation>(), item => item.Table != "PropertyManagerAssignments");
        Assert.Equal(2, operations.OfType<SqlOperation>().Count());
        Assert.Contains(operations.OfType<SqlOperation>(), operation => operation.Sql.Contains("ENABLE ROW LEVEL SECURITY", StringComparison.Ordinal));
        Assert.Contains(operations.OfType<SqlOperation>(), operation => operation.Sql.Contains("REVOKE ALL PRIVILEGES", StringComparison.Ordinal));
        Assert.DoesNotContain(operations, operation => operation is DropIndexOperation or DropColumnOperation or DropTableOperation);
        Assert.DoesNotContain(operations.OfType<AddColumnOperation>(), column => column.Table != "PropertyManagerAssignments");
        Assert.DoesNotContain(operations.OfType<CreateTableOperation>(), created => created.Name != "PropertyManagerAssignments");
    }
}
