using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Api.Controllers;
using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public class RoomAvailabilityTests
{
    private static readonly DateTimeOffset LocalAsOf = new(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(7));
    private static readonly DateTime UtcAsOf = LocalAsOf.UtcDateTime;

    [Fact]
    public async Task GetRentableRoomsAsync_applies_room_booking_and_contract_blockers()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Rooms.AddRange(
            CreateRoom(1),
            CreateRoom(2),
            CreateRoom(3),
            CreateRoom(4),
            CreateRoom(5),
            CreateRoom(6),
            CreateRoom(7),
            CreateRoom(8),
            CreateRoom(9),
            CreateRoom(10),
            CreateRoom(11, RoomStatuses.Reserved),
            CreateRoom(12, RoomStatuses.Occupied),
            CreateRoom(13, RoomStatuses.Maintenance),
            CreateRoom(14),
            CreateRoom(15),
            CreateRoom(16),
            CreateRoom(17),
            CreateRoom(18),
            CreateRoom(19));

        database.Context.Bookings.AddRange(
            CreateBooking(1, 2, "Pending", UtcAsOf.AddSeconds(1)),
            CreateBooking(2, 3, "Pending", UtcAsOf),
            CreateBooking(3, 4, "Pending", UtcAsOf.AddSeconds(-1)),
            CreateBooking(4, 5, "Deposited", UtcAsOf.AddDays(30)),
            CreateBooking(5, 6, "Deposited", UtcAsOf.AddDays(-30)),
            CreateBooking(6, 7, "Cancelled", UtcAsOf.AddDays(30)),
            CreateBooking(7, 14, "Pending", UtcAsOf.AddDays(-1)),
            CreateBooking(8, 15, "Cancelled", UtcAsOf.AddDays(1)),
            CreateBooking(9, 15, "Pending", UtcAsOf.AddSeconds(1)),
            CreateBooking(10, 16, "ManualReview", UtcAsOf.AddDays(-1)),
            CreateBooking(11, 18, "pending", UtcAsOf.AddDays(-1)),
            CreateBooking(12, 19, "Pending", UtcAsOf.AddSeconds(-1)),
            CreateBooking(13, 19, "Pending", UtcAsOf.AddSeconds(-1)),
            CreateBooking(14, 19, "Cancelled", UtcAsOf.AddDays(1)),
            CreateBooking(15, 19, "Cancelled", UtcAsOf.AddDays(1)));

        database.Context.Contracts.AddRange(
            CreateContract(1, 8, "Active", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
            CreateContract(2, 9, "Active", new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1)),
            CreateContract(3, 10, "Active", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)),
            CreateContract(4, 14, "Active", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)),
            CreateContract(5, 17, "Completed", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
        await database.Context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(database.Context));
        var rooms = await service.GetRentableRoomsAsync(LocalAsOf);

        Assert.Equal(new[] { 1, 3, 4, 7, 19 }, rooms.Select(room => room.RoomId).OrderBy(roomId => roomId));
        Assert.All(rooms, room => Assert.Equal("Studio", room.RoomTypeName));
    }

    [Fact]
    public async Task GetPropertyAvailabilityAsync_uses_one_rentable_set_for_count_and_minimum_price()
    {
        await using var context = await CreateInMemoryContextAsync();
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 3, TypeName = "Studio", Price = 2_000_000m });
        context.Rooms.AddRange(
            CreateRoom(1, propertyId: 1, roomTypeId: 1),
            CreateRoom(2, propertyId: 1, roomTypeId: 2),
            CreateRoom(3, propertyId: 1, roomTypeId: 3),
            CreateRoom(4, propertyId: 1, roomTypeId: 1),
            CreateRoom(5, RoomStatuses.Reserved, propertyId: 1, roomTypeId: 1),
            CreateRoom(6, RoomStatuses.Occupied, propertyId: 2, roomTypeId: 1));
        context.Bookings.AddRange(
            CreateBooking(1, 3, "Pending", UtcAsOf.AddSeconds(1)),
            CreateBooking(2, 4, "Pending", UtcAsOf),
            CreateBooking(3, 4, "Cancelled", UtcAsOf.AddDays(1)));
        await context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(context));
        var availability = await service.GetPropertyAvailabilityAsync(LocalAsOf);
        var rentableRooms = await service.GetRentableRoomsAsync(LocalAsOf);

        Assert.Equal(3, Assert.Single(availability, item => item.PropertyId == 1).AvailableRoomCount);
        Assert.Equal(3_000_000m, availability.Single(item => item.PropertyId == 1).StartingPrice);
        Assert.Equal((2, 4_000_000m),
            (rentableRooms.Single(room => room.RoomId == 2).RoomTypeId, rentableRooms.Single(room => room.RoomId == 2).Price));
        Assert.DoesNotContain(rentableRooms, room => room.RoomId == 3);
    }

    [Fact]
    public async Task GetPropertyAvailabilityAsync_returns_zero_and_null_when_no_room_is_rentable()
    {
        await using var context = await CreateInMemoryContextAsync();
        context.Rooms.Add(CreateRoom(1, RoomStatuses.Occupied, propertyId: 2));
        await context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(context));
        var availability = await service.GetPropertyAvailabilityAsync(LocalAsOf);

        Assert.Equal(3, availability.Count);
        Assert.All(availability.Where(item => item.PropertyId is 2 or 3), item =>
        {
            Assert.Equal(0, item.AvailableRoomCount);
            Assert.Null(item.StartingPrice);
        });
    }

    [Fact]
    public void RentableRoomDto_contains_only_allowlisted_room_fields()
    {
        var propertyNames = typeof(RentableRoomDto).GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            new[] { "AreaSqm", "Price", "PropertyId", "RoomCode", "RoomId", "RoomTypeId", "RoomTypeName" },
            propertyNames);
    }

    [Fact]
    public void GetPropertyAvailability_generates_postgresql_server_side_aggregates()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=phase2a;Username=phase2a")
            .Options;
        using var context = new AppDbContext(options);

        var sql = RoomAvailabilityQueries
            .GetPropertyAvailability(context.Properties, context.Rooms, LocalAsOf)
            .ToQueryString();

        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIN(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CustomerId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContractId", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoomUpdate_accepts_reserved_and_keeps_owner_and_role_checks()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Rooms.Add(CreateRoom(1));
        await database.Context.SaveChangesAsync();
        var service = new RoomService(new RoomRepository(database.Context));

        var updated = await service.UpdateAsync(1, new UpdateRoomDto(1, "A-1", 20, RoomStatuses.Reserved), 10, false);

        Assert.Equal(RoomStatuses.Reserved, updated!.Status);
        await Assert.ThrowsAsync<RoomForbiddenException>(() =>
            service.UpdateAsync(1, new UpdateRoomDto(1, "A-1", 20, RoomStatuses.Available), 20, false));
        await Assert.ThrowsAsync<RoomValidationException>(() =>
            service.UpdateAsync(1, new UpdateRoomDto(1, "A-1", 20, "Unknown"), 10, false));
        await Assert.ThrowsAsync<RoomValidationException>(() =>
            service.UpdateAsync(1, new UpdateRoomDto(1, " ", 20, RoomStatuses.Available), 10, false));
        await Assert.ThrowsAsync<RoomValidationException>(() =>
            service.UpdateAsync(1, new UpdateRoomDto(1, "A-1", 0, RoomStatuses.Available), 10, false));
        await Assert.ThrowsAsync<RoomTypeNotFoundException>(() =>
            service.UpdateAsync(1, new UpdateRoomDto(999, "A-1", 20, RoomStatuses.Available), 10, false));
        Assert.Null(await service.UpdateAsync(999, new UpdateRoomDto(1, "A-999", 20, RoomStatuses.Available), 10, false));

        var adminUpdated = await service.UpdateAsync(
            1,
            new UpdateRoomDto(1, "A-1", 20, RoomStatuses.Available),
            99,
            true);
        Assert.Equal(RoomStatuses.Available, adminUpdated!.Status);

        var updateAction = typeof(RoomsController).GetMethod(nameof(RoomsController.Update))!;
        var authorization = Assert.Single(updateAction.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal("Admin,Manager", ((AuthorizeAttribute)authorization).Roles);
    }

    private static Rooms CreateRoom(
        int roomId,
        string status = RoomStatuses.Available,
        int propertyId = 1,
        int roomTypeId = 1) =>
        new()
        {
            RoomId = roomId,
            PropertyId = propertyId,
            RoomTypeId = roomTypeId,
            RoomCode = $"A-{roomId}",
            AreaSqm = 20,
            Status = status
        };

    private static Bookings CreateBooking(int bookingId, int roomId, string status, DateTime expiryDate) =>
        new()
        {
            BookingId = bookingId,
            RoomId = roomId,
            CustomerId = 10,
            Status = status,
            ExpiryDate = expiryDate
        };

    private static Contracts CreateContract(
        int contractId,
        int roomId,
        string status,
        DateOnly startDate,
        DateOnly endDate) =>
        new()
        {
            ContractId = contractId,
            RoomId = roomId,
            CustomerId = 10,
            Status = status,
            StartDate = startDate,
            EndDate = endDate
        };

    private static async Task<AppDbContext> CreateInMemoryContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        await SeedBaseDataAsync(context);
        return context;
    }

    private static async Task SeedBaseDataAsync(AppDbContext context)
    {
        context.Roles.Add(new Roles { RoleId = 1, RoleName = "Owner" });
        context.Users.Add(new Users
        {
            UserId = 10,
            RoleId = 1,
            FullName = "Property Owner",
            Email = "owner@example.test",
            PasswordHash = "test-hash",
            PhoneNumber = "0000000000",
            IsActive = true
        });
        context.Properties.AddRange(
            new Property { PropertyId = 1, OwnerId = 10, PropertyName = "Property One" },
            new Property { PropertyId = 2, OwnerId = 10, PropertyName = "Property Two" },
            new Property { PropertyId = 3, OwnerId = 10, PropertyName = "Property Three" });
        context.RoomTypes.AddRange(
            new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m },
            new RoomTypes { RoomTypeId = 2, TypeName = "Studio", Price = 4_000_000m });
        await context.SaveChangesAsync();
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context
        {
            get;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await SeedBaseDataAsync(context);

            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
