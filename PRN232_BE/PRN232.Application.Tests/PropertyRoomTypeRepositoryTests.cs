using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyRoomTypeRepositoryTests
{
    [Fact]
    public async Task GetAllByPropertyIdAsync_reads_scoped_configurations_with_room_counts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);

        var repository = new PropertyRoomTypeRepository(context);

        var rows = await repository.GetAllByPropertyIdAsync(
            propertyId: 1,
            asOfUtc: DateTimeOffset.UtcNow);

        Assert.Equal(new[] { 1, 2 }, rows.Select(row => row.RoomTypeId));
        var alpha = rows[0];
        Assert.Equal(("Alpha", 3_000_000m, 2, 1),
            (alpha.RoomTypeName, alpha.MonthlyRent, alpha.TotalRoomCount, alpha.AvailableRoomCount));
        var beta = rows[1];
        Assert.Equal(("Beta", 5_000_000m, 1, 1),
            (beta.RoomTypeName, beta.MonthlyRent, beta.TotalRoomCount, beta.AvailableRoomCount));

        var selected = await repository.GetByIdAsync(
            propertyId: 1,
            roomTypeId: 1,
            asOfUtc: DateTimeOffset.UtcNow);
        Assert.NotNull(selected);
        Assert.Equal("Alpha", selected.RoomTypeName);
        Assert.Null(await repository.GetByIdAsync(2, 2, DateTimeOffset.UtcNow));
    }

    private static async Task SeedAsync(AppDbContext context)
    {
        context.Roles.Add(new Roles { RoleId = 1, RoleName = "Admin" });
        context.Users.Add(new Users
        {
            UserId = 1,
            RoleId = 1,
            FullName = "Owner",
            Email = "owner@example.test",
            IsActive = true
        });
        context.Properties.AddRange(
            new Property
            {
                PropertyId = 1,
                OwnerId = 1,
                PropertyName = "Property One",
                AddressText = "1 Test Street",
                Ward = "Ward 1",
                District = "District 1",
                City = "City"
            },
            new Property
            {
                PropertyId = 2,
                OwnerId = 1,
                PropertyName = "Property Two",
                AddressText = "2 Test Street",
                Ward = "Ward 1",
                District = "District 1",
                City = "City"
            });
        context.RoomTypes.AddRange(
            new RoomTypes { RoomTypeId = 1, TypeName = "Alpha", Price = 0m },
            new RoomTypes { RoomTypeId = 2, TypeName = "Beta", Price = 0m });
        context.PropertyRoomTypes.AddRange(
            new PropertyRoomTypes
            {
                PropertyId = 1,
                RoomTypeId = 1,
                MonthlyRent = 3_000_000m,
                IsActive = true
            },
            new PropertyRoomTypes
            {
                PropertyId = 1,
                RoomTypeId = 2,
                MonthlyRent = 5_000_000m,
                IsActive = true
            },
            new PropertyRoomTypes
            {
                PropertyId = 2,
                RoomTypeId = 1,
                MonthlyRent = 4_000_000m,
                IsActive = true
            });
        context.Rooms.AddRange(
            Room(1, 1, 1, "A101", "Available"),
            Room(2, 1, 1, "A102", "Occupied"),
            Room(3, 1, 2, "B101", "Available"),
            Room(4, 2, 1, "C101", "Available"));
        await context.SaveChangesAsync();
    }

    private static Rooms Room(
        int roomId,
        int propertyId,
        int roomTypeId,
        string roomCode,
        string status) => new()
    {
        RoomId = roomId,
        PropertyId = propertyId,
        RoomTypeId = roomTypeId,
        RoomCode = roomCode,
        AreaSqm = 20m,
        Status = status
    };
}
