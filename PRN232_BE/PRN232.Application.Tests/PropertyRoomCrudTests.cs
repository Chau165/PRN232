using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyRoomCrudTests
{
    [Fact]
    public async Task CreateRoom_rejects_manager_even_when_manager_owns_the_property()
    {
        await using var context = await CreateContextAsync();
        var service = new RoomService(new RoomRepository(context));

        await Assert.ThrowsAsync<RoomForbiddenException>(() => service.CreateAsync(
            new CreateRoomDto(1, 1, "A-101", 24.5m),
            requestingUserId: 10,
            isAdmin: false));
    }

    [Fact]
    public async Task CreateRoom_rejects_duplicate_code_in_the_same_property()
    {
        await using var context = await CreateContextAsync();
        context.Rooms.Add(new Rooms
        {
            RoomId = 1,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = "A-101",
            AreaSqm = 20,
            Status = RoomStatuses.Available
        });
        await context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(context));

        await Assert.ThrowsAsync<RoomValidationException>(() => service.CreateAsync(
            new CreateRoomDto(1, 1, " a-101 ", 24.5m),
            requestingUserId: 10,
            isAdmin: true));
    }

    [Fact]
    public async Task UpdateRoom_changes_configuration_without_changing_status()
    {
        await using var context = await CreateContextAsync();
        context.Rooms.Add(new Rooms
        {
            RoomId = 1,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = "A-101",
            AreaSqm = 20,
            Status = RoomStatuses.Occupied
        });
        await context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(context));
        var updated = await service.UpdateAsync(
            1,
            new UpdateRoomDto(1, "A-102", 25),
            requestingUserId: 10,
            isAdmin: true);

        Assert.NotNull(updated);
        Assert.Equal("A-102", updated.RoomCode);
        Assert.Equal(25, updated.AreaSqm);
        Assert.Equal(RoomStatuses.Occupied, updated.Status);
    }

    // Rebuild 2.1: occupancy defaults to what the area allows (8 m² per person).
    [Fact]
    public async Task CreateRoom_without_occupancy_uses_the_area_limit_and_keeps_floor_and_description()
    {
        await using var context = await CreateContextAsync();
        var service = new RoomService(new RoomRepository(context), new PropertyRoomTypeRepository(context));

        var created = await service.CreateAsync(
            new CreateRoomDto(1, 1, "A-201", 25m, Floor: 2, Description: "  Ban công hướng đông  "),
            requestingUserId: 10,
            isAdmin: true);

        Assert.Equal(3, created.MaxOccupants);
        Assert.Equal(2, created.Floor);
        Assert.Equal("Ban công hướng đông", created.Description);
    }

    [Theory]
    [InlineData(25, 2)]   // area still allows 3 -> the stored 2 is kept
    [InlineData(12, 1)]   // area now allows only 1 -> lowered
    public async Task UpdateRoom_without_occupancy_keeps_it_unless_the_new_area_is_too_small(
        decimal newArea, int expected)
    {
        await using var context = await CreateContextAsync();
        context.Rooms.Add(new Rooms
        {
            RoomId = 1,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = "A-101",
            AreaSqm = 25,
            MaxOccupants = 2,
            Status = RoomStatuses.Available
        });
        await context.SaveChangesAsync();

        var updated = await new RoomService(new RoomRepository(context)).UpdateAsync(
            1, new UpdateRoomDto(1, "A-101", newArea), requestingUserId: 10, isAdmin: true);

        Assert.Equal(expected, updated!.MaxOccupants);
    }

    [Fact]
    public async Task DeleteRoom_rejects_an_occupied_room_even_without_rows_in_child_tables()
    {
        await using var context = await CreateContextAsync();
        context.Rooms.Add(new Rooms
        {
            RoomId = 1,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = "A-101",
            AreaSqm = 20,
            Status = RoomStatuses.Occupied
        });
        await context.SaveChangesAsync();

        var service = new RoomService(new RoomRepository(context));

        await Assert.ThrowsAsync<RoomHasDependentsException>(() => service.DeleteAsync(
            1,
            requestingUserId: 10,
            isAdmin: true));
    }

    [Fact]
    public async Task DeleteProperty_rejects_a_property_with_manager_assignment_history()
    {
        await using var context = await CreateContextAsync();
        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            PropertyId = 1,
            ManagerId = 11
        });
        await context.SaveChangesAsync();

        var service = new PropertyService(new PropertyRepository(context));

        await Assert.ThrowsAsync<PropertyHasRoomsException>(() => service.DeleteAsync(
            1,
            requestingUserId: 10,
            isAdmin: true));
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"property-room-crud-{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        context.Users.AddRange(
            new Users
            {
                UserId = 10,
                RoleId = 1,
                FullName = "Admin",
                Email = "admin@example.test",
                IsActive = true
            },
            new Users
            {
                UserId = 11,
                RoleId = 2,
                FullName = "Manager",
                Email = "manager@example.test",
                IsActive = true
            });
        context.Properties.Add(new Property
        {
            PropertyId = 1,
            OwnerId = 10,
            PropertyName = "Nhà trọ Bình An",
            AddressText = "12 Đường số 5",
            Ward = "Phường 1",
            District = "Thủ Đức",
            City = "TP. Hồ Chí Minh",
        });
        context.RoomTypes.Add(new RoomTypes
        {
            RoomTypeId = 1,
            TypeName = "Studio",
            Price = 3_000_000m
        });
        context.PropertyRoomTypes.Add(new PropertyRoomTypes
        {
            PropertyId = 1,
            RoomTypeId = 1,
            MonthlyRent = 3_000_000m,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return context;
    }
}
