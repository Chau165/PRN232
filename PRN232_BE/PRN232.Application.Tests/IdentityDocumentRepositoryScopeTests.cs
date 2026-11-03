using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

// Manager chỉ được thấy hồ sơ CCCD của khách có booking chưa huỷ hoặc hợp đồng ở khu trọ của mình.
public sealed class IdentityDocumentRepositoryScopeTests
{
    [Fact]
    public async Task Owner_scope_includes_booking_and_contract_customers_and_excludes_cancelled_and_other_owners()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        Seed(context);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new IdentityDocumentRepository(context);

        var managerA = await repository.GetAllAsync(null, 7);
        var managerB = await repository.GetAllAsync(null, 8);
        var admin = await repository.GetAllAsync(null, null);

        Assert.Equal([10, 11], managerA.Select(document => document.CustomerId).Order());
        Assert.Equal([13], managerB.Select(document => document.CustomerId));
        Assert.Equal(4, admin.Count);
        Assert.True(await repository.IsCustomerOfOwnerAsync(11, 7));
        Assert.False(await repository.IsCustomerOfOwnerAsync(12, 7));
        Assert.False(await repository.IsCustomerOfOwnerAsync(13, 7));
    }

    private static void Seed(AppDbContext context)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = "Customer" },
            new Roles { RoleId = 2, RoleName = "Manager" });
        context.Users.AddRange(
            User(7, 2, "manager-a@example.test"),
            User(8, 2, "manager-b@example.test"),
            User(10, 1, "booking@example.test"),
            User(11, 1, "contract@example.test"),
            User(12, 1, "cancelled@example.test"),
            User(13, 1, "other-owner@example.test"));
        context.Properties.AddRange(
            new Property { PropertyId = 1, OwnerId = 7, PropertyName = "A" },
            new Property { PropertyId = 2, OwnerId = 8, PropertyName = "B" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", Status = "Available" },
            new Rooms { RoomId = 2, PropertyId = 2, RoomTypeId = 1, RoomCode = "B101", Status = "Available" });
        context.Bookings.AddRange(
            Booking(1, 1, 10, "Pending"),
            Booking(2, 1, 12, "Cancelled"),
            Booking(3, 2, 13, "Deposited"));
        context.Contracts.Add(new Contracts
        {
            ContractId = 1,
            RoomId = 1,
            CustomerId = 11,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRent = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = "Active"
        });
        context.IdentityDocuments.AddRange(
            Document(1, 10), Document(2, 11), Document(3, 12), Document(4, 13));
    }

    private static Users User(int id, int roleId, string email) =>
        new() { UserId = id, RoleId = roleId, FullName = email, Email = email, PasswordHash = "x", IsActive = true };

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

    private static IdentityDocuments Document(int id, int customerId) =>
        new() { IdentityDocumentId = id, CustomerId = customerId, CCCDNumber = $"0000000000{id:D2}", Status = "Pending" };
}
