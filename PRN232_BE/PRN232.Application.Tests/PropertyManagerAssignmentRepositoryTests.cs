using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyManagerAssignmentRepositoryTests
{
    [Fact]
    public void Assignment_model_uses_only_the_composite_property_manager_key()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new AppDbContext(options);
        var assignment = context.Model.FindEntityType(typeof(PropertyManagerAssignment));

        Assert.NotNull(assignment);
        Assert.Equal(
            new[] { nameof(PropertyManagerAssignment.PropertyId), nameof(PropertyManagerAssignment.ManagerId) },
            assignment.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(PropertyManagerAssignment.ManagerId), nameof(PropertyManagerAssignment.PropertyId) },
            assignment.GetProperties().Select(property => property.Name).OrderBy(name => name));
        Assert.All(assignment.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        var contractProperties = context.Model.FindEntityType(typeof(Contracts))!
            .GetProperties()
            .Select(property => property.Name);
        Assert.DoesNotContain("PrepaidRentAmount", contractProperties);
        Assert.DoesNotContain("LessorUserId", contractProperties);
        Assert.DoesNotContain("LessorNameSnapshot", contractProperties);
        Assert.DoesNotContain("LessorAddressSnapshot", contractProperties);

        var invoiceProperties = context.Model.FindEntityType(typeof(Invoices))!
            .GetProperties()
            .Select(property => property.Name);
        Assert.DoesNotContain("PrepaidRentApplied", invoiceProperties);
        Assert.Null(context.Model.FindEntityType("PRN232.Domain.Entities.PrepaidRentCreditApplications"));
    }

    [Fact]
    public async Task Revoke_removes_the_assignment_without_deleting_the_property_or_manager()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Roles.AddRange(
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager", true),
            User(9, 3, "Admin", true));
        context.Properties.Add(Property(1, "Sunrise"));
        context.PropertyManagerAssignments.Add(Assignment(1, 7));
        await context.SaveChangesAsync();

        var result = await new PropertyManagerAssignmentRepository(context).RevokeActiveAsync(
            1,
            7,
            9,
            CancellationToken.None);

        Assert.Equal(RevokePropertyManagerAssignmentResult.Revoked, result);
        Assert.Empty(await context.PropertyManagerAssignments.ToListAsync());
        Assert.Single(await context.Properties.ToListAsync());
        Assert.Equal("Manager", (await context.Users.SingleAsync(user => user.UserId == 7)).FullName);
    }

    [Fact]
    public async Task Managed_properties_include_only_active_managers_with_assignments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Active Manager", true),
            User(8, 2, "Inactive Manager", false),
            User(9, 3, "Property Owner", true),
            User(10, 1, "Not a Manager", true));
        context.Properties.AddRange(
            Property(1, "Active assignment"),
            Property(2, "Inactive manager"),
            Property(3, "Wrong role"));
        context.PropertyManagerAssignments.AddRange(
            Assignment(1, 7),
            Assignment(2, 8),
            Assignment(3, 10));
        await context.SaveChangesAsync();

        var repository = new PropertyManagerAssignmentRepository(context);

        var activeManagerProperties = await repository.GetManagedPropertiesAsync(7, true, CancellationToken.None);
        var inactiveManagerProperties = await repository.GetManagedPropertiesAsync(8, false, CancellationToken.None);
        var nonManagerProperties = await repository.GetManagedPropertiesAsync(10, false, CancellationToken.None);

        Assert.Equal([1], activeManagerProperties.Select(property => property.PropertyId));
        Assert.Equal([2], inactiveManagerProperties.Select(property => property.PropertyId));
        Assert.Empty(nonManagerProperties);
    }

    [Fact]
    public async Task Assignments_support_multiple_managers_and_properties_without_changing_owner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Roles.AddRange(
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager One", true),
            User(8, 2, "Manager Two", true),
            User(9, 3, "Admin Owner", true));
        context.Properties.AddRange(Property(1, "Property One"), Property(2, "Property Two"));
        await context.SaveChangesAsync();

        var repository = new PropertyManagerAssignmentRepository(context);

        var firstAssignment = await repository.AddIfActiveAssignmentAbsentAsync(
            Assignment(1, 7), 9, CancellationToken.None);
        var secondAssignment = await repository.AddIfActiveAssignmentAbsentAsync(
            Assignment(1, 8), 9, CancellationToken.None);
        var thirdAssignment = await repository.AddIfActiveAssignmentAbsentAsync(
            Assignment(2, 8), 9, CancellationToken.None);

        Assert.True(firstAssignment!.ManagerIsActive);
        Assert.True(secondAssignment!.ManagerIsActive);
        Assert.True(thirdAssignment!.ManagerIsActive);
        Assert.Equal([7, 8], (await repository.GetByPropertyIdAsync(1, CancellationToken.None))
            .Select(assignment => assignment.ManagerId));
        Assert.Equal([1, 2], (await repository.GetManagedPropertiesAsync(8, true, CancellationToken.None))
            .Select(property => property.PropertyId));

        var revokeResult = await repository.RevokeActiveAsync(1, 7, 9, CancellationToken.None);

        Assert.Equal(RevokePropertyManagerAssignmentResult.Revoked, revokeResult);
        Assert.Equal([8], (await repository.GetByPropertyIdAsync(1, CancellationToken.None))
            .Select(assignment => assignment.ManagerId));
        Assert.Equal(9, (await context.Properties.SingleAsync(property => property.PropertyId == 1)).OwnerId);
        Assert.Equal(9, (await context.Properties.SingleAsync(property => property.PropertyId == 2)).OwnerId);
    }

    [Fact]
    public async Task Batch_lookup_loads_assignments_for_many_properties_in_a_single_query()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Roles.AddRange(
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager One", true),
            User(8, 2, "Manager Two", true),
            User(9, 3, "Admin Owner", true));
        context.Properties.AddRange(
            Property(1, "Property One"),
            Property(2, "Property Two"),
            Property(3, "Property Three"));
        context.PropertyManagerAssignments.AddRange(
            Assignment(1, 7),
            Assignment(1, 8),
            Assignment(2, 8),
            Assignment(3, 7));
        await context.SaveChangesAsync();
        var repository = new PropertyManagerAssignmentRepository(context);

        counter.Count = 0;
        var filtered = await repository.GetByPropertyIdsAsync([1, 2], CancellationToken.None);

        Assert.Equal(1, counter.Count);
        Assert.Equal(
            [(1, 7), (1, 8), (2, 8)],
            filtered.Select(assignment => (assignment.PropertyId, assignment.ManagerId)));

        counter.Count = 0;
        var all = await repository.GetByPropertyIdsAsync(null, CancellationToken.None);

        Assert.Equal(1, counter.Count);
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public async Task Batch_manager_lookup_loads_properties_for_many_managers_in_a_single_query()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager One", true),
            User(8, 2, "Manager Two", false),
            User(9, 3, "Admin Owner", true),
            User(10, 1, "Not a Manager", true));
        context.Properties.AddRange(
            Property(1, "Property One"),
            Property(2, "Property Two"),
            Property(3, "Property Three"));
        context.PropertyManagerAssignments.AddRange(
            Assignment(2, 7),
            Assignment(1, 7),
            Assignment(3, 8),
            Assignment(1, 10));
        await context.SaveChangesAsync();
        var repository = new PropertyManagerAssignmentRepository(context);

        counter.Count = 0;
        var filtered = await repository.GetManagedPropertiesForManagersAsync([7, 8, 10], CancellationToken.None);

        Assert.Equal(1, counter.Count);
        // Inactive managers are still listed for Admin; non-Manager accounts are not.
        Assert.Equal(
            [(7, 1), (7, 2), (8, 3)],
            filtered.Select(row => (row.ManagerId, row.Property.PropertyId)));
        Assert.Equal("Admin Owner", filtered[0].Property.OwnerName);

        counter.Count = 0;
        var all = await repository.GetManagedPropertiesForManagersAsync(null, CancellationToken.None);

        Assert.Equal(1, counter.Count);
        Assert.Equal(3, all.Count);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static Users User(int id, int roleId, string name, bool isActive) => new()
    {
        UserId = id,
        RoleId = roleId,
        FullName = name,
        Email = $"{id}@example.test",
        PasswordHash = "test",
        IsActive = isActive
    };

    private static Property Property(int id, string name) => new()
    {
        PropertyId = id,
        OwnerId = 9,
        PropertyName = name
    };

    private static PropertyManagerAssignment Assignment(int propertyId, int managerId) => new()
    {
        PropertyId = propertyId,
        ManagerId = managerId
    };
}
