using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ViewingScheduleRepositoryTests
{
    [Fact]
    public async Task Manager_read_scope_and_decision_recheck_follow_active_property_assignment()
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
            User(7, 2, "Manager"),
            User(8, 2, "Other Manager"),
            User(9, 3, "Admin"),
            User(10, 1, "Customer"));
        context.Properties.AddRange(
            new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Assigned property" },
            new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Previously assigned property" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 100m });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", Status = "Available" },
            new Rooms { RoomId = 2, PropertyId = 2, RoomTypeId = 1, RoomCode = "B101", Status = "Available" });
        context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment
            {
                PropertyId = 1,
                ManagerId = 7
            },
            new PropertyManagerAssignment
            {
                PropertyId = 2,
                ManagerId = 8
            });
        context.ViewingSchedules.AddRange(
            new ViewingSchedules { ViewingId = 1, RoomId = 1, CustomerId = 10, AssignedTo = 7, Status = ViewingStatuses.Pending },
            new ViewingSchedules { ViewingId = 2, RoomId = 2, CustomerId = 10, Status = ViewingStatuses.Pending });
        await context.SaveChangesAsync();

        var repository = new ViewingScheduleRepository(context);

        var managerRows = await repository.GetAllAsync(null, 7, null);
        var otherManagerRows = await repository.GetAllAsync(null, 8, null);
        Assert.Equal([1], managerRows.Select(viewing => viewing.ViewingId));
        Assert.Empty(otherManagerRows);

        context.PropertyManagerAssignments.Remove(
            await context.PropertyManagerAssignments.SingleAsync(assignment => assignment.PropertyId == 1));
        await context.SaveChangesAsync();

        var decided = await repository.DecideAsync(
            1,
            ViewingStatuses.Approved,
            7,
            "Manager note",
            7,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            []);

        Assert.False(decided);
        context.ChangeTracker.Clear();
        var viewing = await context.ViewingSchedules.SingleAsync(item => item.ViewingId == 1);
        Assert.Equal(ViewingStatuses.Pending, viewing.Status);
        Assert.Equal(7, viewing.AssignedTo);
    }

    [Fact]
    public async Task Decision_rolls_back_schedule_update_when_notification_insert_fails()
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
            User(7, 2, "Manager"),
            User(9, 3, "Admin"),
            User(10, 1, "Customer"));
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Sunrise" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 100m });
        context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", Status = "Available" });
        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            PropertyId = 1,
            ManagerId = 7
        });
        context.ViewingSchedules.Add(new ViewingSchedules
        {
            ViewingId = 1,
            RoomId = 1,
            CustomerId = 10,
            Status = ViewingStatuses.Pending
        });
        await context.SaveChangesAsync();

        var repository = new ViewingScheduleRepository(context);
        var invalidNotification = new Notifications
        {
            UserId = 999,
            Title = "Will violate the notification user FK",
            Type = NotificationTypes.Viewing,
            RelatedEntityId = 1,
            IsRead = false
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.DecideAsync(
            1,
            ViewingStatuses.Approved,
            7,
            "Assignment note",
            9,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            [invalidNotification]));

        context.ChangeTracker.Clear();
        var viewing = await context.ViewingSchedules.SingleAsync();
        Assert.Equal(ViewingStatuses.Pending, viewing.Status);
        Assert.Null(viewing.AssignedTo);
        Assert.Null(viewing.ManagerNote);
        Assert.Null(viewing.RespondedBy);
        Assert.Null(viewing.RespondedAt);
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    private static Users User(int id, int roleId, string name) => new()
    {
        UserId = id,
        RoleId = roleId,
        FullName = name,
        Email = $"{id}@example.test",
        PasswordHash = "test",
        IsActive = true
    };
}
