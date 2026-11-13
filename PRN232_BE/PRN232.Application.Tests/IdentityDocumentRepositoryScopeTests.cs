using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

// A3: a Manager only sees identity documents of customers who have a non-cancelled booking or a
// contract on a property assigned to them in PropertyManagerAssignments. Property.OwnerId plays no role.
public sealed class IdentityDocumentRepositoryScopeTests
{
    [Fact]
    public async Task Manager_scope_follows_property_assignments_for_bookings_and_contracts()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new IdentityDocumentRepository(context);

        var managerA = await repository.GetAllAsync(null, 7);
        var managerB = await repository.GetAllAsync(null, 8);
        var unassignedManager = await repository.GetAllAsync(null, 16);
        var admin = await repository.GetAllAsync(null, null);

        // Property 1 is assigned to A; property 2 to B; property 3 to both.
        Assert.Equal([10, 11, 15], managerA.Select(document => document.CustomerId).Order());
        Assert.Equal([13, 15], managerB.Select(document => document.CustomerId).Order());
        Assert.Empty(unassignedManager);
        Assert.Equal(6, admin.Count);
    }

    [Fact]
    public async Task Access_check_uses_assignments_not_property_owner()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new IdentityDocumentRepository(context);

        Assert.True(await repository.IsCustomerOfManagerAsync(10, 7));   // booking, assigned property
        Assert.True(await repository.IsCustomerOfManagerAsync(11, 7));   // contract, assigned property
        Assert.True(await repository.IsCustomerOfManagerAsync(15, 8));   // property shared by A and B
        Assert.False(await repository.IsCustomerOfManagerAsync(12, 7));  // only a cancelled booking
        Assert.False(await repository.IsCustomerOfManagerAsync(13, 7));  // another manager's property
        // Manager 16 is the legacy OwnerId of property 4 but has no assignment there.
        Assert.False(await repository.IsCustomerOfManagerAsync(14, 16));
        Assert.False(await repository.IsCustomerOfManagerAsync(14, 7));
    }

    private static async Task<AppDbContext> CreateSeededContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        Seed(context);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    private static void Seed(AppDbContext context)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = "Customer" },
            new Roles { RoleId = 2, RoleName = "Manager" },
            new Roles { RoleId = 3, RoleName = "Admin" });
        context.Users.AddRange(
            User(7, 2, "manager-a@example.test"),
            User(8, 2, "manager-b@example.test"),
            User(16, 2, "legacy-owner-manager@example.test"),
            User(9, 3, "admin@example.test"),
            User(10, 1, "booking@example.test"),
            User(11, 1, "contract@example.test"),
            User(12, 1, "cancelled@example.test"),
            User(13, 1, "other-manager@example.test"),
            User(14, 1, "legacy-owner-property@example.test"),
            User(15, 1, "shared-property@example.test"));
        context.Properties.AddRange(
            new Property { PropertyId = 1, OwnerId = 9, PropertyName = "A" },
            new Property { PropertyId = 2, OwnerId = 9, PropertyName = "B" },
            new Property { PropertyId = 3, OwnerId = 9, PropertyName = "Shared" },
            // Legacy row: OwnerId points at a Manager, but nobody is assigned to it.
            new Property { PropertyId = 4, OwnerId = 16, PropertyName = "Legacy owner" });
        context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 },
            new PropertyManagerAssignment { PropertyId = 3, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 3, ManagerId = 8 });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.AddRange(
            Room(1, 1, "A101"), Room(2, 2, "B101"), Room(3, 3, "S101"), Room(4, 4, "L101"));
        context.Bookings.AddRange(
            Booking(1, 1, 10, "Pending"),
            Booking(2, 1, 12, "Cancelled"),
            Booking(3, 2, 13, "Deposited"),
            Booking(4, 4, 14, "Pending"));
        context.Contracts.AddRange(Contract(1, 1, 11), Contract(2, 3, 15));
        context.IdentityDocuments.AddRange(
            Document(1, 10), Document(2, 11), Document(3, 12),
            Document(4, 13), Document(5, 14), Document(6, 15));
    }

    private static Users User(int id, int roleId, string email) =>
        new() { UserId = id, RoleId = roleId, FullName = email, Email = email, PasswordHash = "x", IsActive = true };

    private static Rooms Room(int id, int propertyId, string code) =>
        new() { RoomId = id, PropertyId = propertyId, RoomTypeId = 1, RoomCode = code, Status = "Available" };

    private static Bookings Booking(int id, int roomId, int customerId, string status) =>
        new()
        {
            BookingId = id,
            RoomId = roomId,
            CustomerId = customerId,
            DepositAmount = 3_000_000m,
            Status = status,
            ExpiryDate = DateTime.UtcNow.AddDays(1)
        };

    private static Contracts Contract(int id, int roomId, int customerId) =>
        new()
        {
            ContractId = id,
            RoomId = roomId,
            CustomerId = customerId,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRent = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = "Active"
        };

    private static IdentityDocuments Document(int id, int customerId) =>
        new() { IdentityDocumentId = id, CustomerId = customerId, CCCDNumber = $"0000000000{id:D2}", Status = "Pending" };
}
