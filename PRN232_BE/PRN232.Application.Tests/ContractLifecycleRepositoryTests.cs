using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ContractLifecycleRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 5);

    [Fact]
    public async Task ExtendAsync_updates_the_same_contract_and_notifies_in_the_relational_transaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractAsync(context);

        var result = await new ContractRepository(context).ExtendAsync(
            1,
            7,
            new DateOnly(2027, 11, 30),
            Today,
            Now,
            Notification(),
            CancellationToken.None);

        Assert.Equal(ContractMutationStatus.Updated, result.Status);
        Assert.Equal(1, result.Contract!.ContractId);
        Assert.Equal(new DateOnly(2027, 11, 30), result.Contract.EndDate);
        Assert.Equal(ContractStatuses.Active, result.Contract.Status);
        Assert.Equal(1, await context.Contracts.CountAsync());
        Assert.Equal(0, await context.Bookings.CountAsync());
        Assert.Equal(RoomStatuses.Occupied, (await context.Rooms.SingleAsync()).Status);
        Assert.Single(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task TerminateAsync_keeps_unpaid_invoices_and_payments_and_releases_a_ready_room()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractAsync(context);
        var invoice = new Invoices
        {
            ContractId = 1,
            InvoiceMonth = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 15),
            Status = InvoiceStatuses.Overdue
        };
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Rent,
            Description = "Rent",
            Amount = 3_237_500m
        });
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        context.Payments.Add(new Payments
        {
            UserId = 10,
            InvoiceId = invoice.InvoiceId,
            Amount = 500_000m,
            PaymentMethod = "Cash",
            Status = PaymentStatuses.Success
        });
        await context.SaveChangesAsync();

        var result = await new ContractRepository(context).TerminateAsync(
            1,
            7,
            roomReadyForRental: true,
            outstandingDebtAcknowledged: true,
            Now,
            Notification(),
            CancellationToken.None);

        Assert.Equal(ContractMutationStatus.Updated, result.Status);
        Assert.Equal(ContractStatuses.Terminated, result.Contract!.Status);
        Assert.Equal(2_737_500m, result.OutstandingDebt);
        Assert.Equal(new DateOnly(2027, 10, 31), result.Contract.EndDate);
        Assert.Equal(RoomStatuses.Available, (await context.Rooms.SingleAsync()).Status);
        Assert.Equal(InvoiceStatuses.Overdue, (await context.Invoices.SingleAsync()).Status);
        Assert.Equal(500_000m, (await context.Payments.SingleAsync()).Amount);
        Assert.Single(await context.Notifications.ToListAsync());

        var repeat = await new ContractRepository(context).TerminateAsync(
            1,
            7,
            roomReadyForRental: true,
            outstandingDebtAcknowledged: true,
            Now,
            Notification(),
            CancellationToken.None);

        Assert.Equal(ContractMutationStatus.Conflict, repeat.Status);
        Assert.Single(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task TerminateAsync_keeps_a_room_unrentable_until_manager_marks_it_ready()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractAsync(context);

        var result = await new ContractRepository(context).TerminateAsync(
            1,
            7,
            roomReadyForRental: false,
            outstandingDebtAcknowledged: true,
            Now,
            Notification(),
            CancellationToken.None);

        Assert.Equal(ContractMutationStatus.Updated, result.Status);
        Assert.Equal(RoomStatuses.Maintenance, (await context.Rooms.SingleAsync()).Status);
        Assert.Empty(await RoomAvailabilityQueries.GetRentableRooms(context.Rooms, Now).ToListAsync());

        var ready = await new ContractRepository(context).MarkTerminatedRoomReadyAsync(
            1,
            7,
            Now,
            CancellationToken.None);

        Assert.Equal(ContractMutationStatus.Updated, ready);
        Assert.Equal(RoomStatuses.Available, (await context.Rooms.SingleAsync()).Status);
        Assert.Single(await RoomAvailabilityQueries.GetRentableRooms(context.Rooms, Now).ToListAsync());
    }

    [Fact]
    public async Task TerminateAsync_rolls_back_contract_room_and_notification_when_persistence_fails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractAsync(context);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectContractNotification
            BEFORE INSERT ON "Notifications"
            BEGIN
                SELECT RAISE(ABORT, 'notification persistence rejected by test');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => new ContractRepository(context).TerminateAsync(
            1,
            7,
            roomReadyForRental: true,
            outstandingDebtAcknowledged: true,
            Now,
            Notification(),
            CancellationToken.None));

        Assert.Equal(ContractStatuses.Active, await context.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == 1)
            .Select(contract => contract.Status)
            .SingleAsync());
        Assert.Equal(RoomStatuses.Occupied, await context.Rooms.AsNoTracking()
            .Select(room => room.Status)
            .SingleAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    private static async Task SeedContractAsync(AppDbContext context)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager"),
            User(9, 3, "Owner"),
            User(10, 1, "Customer"));
        context.Properties.Add(new Property
        {
            PropertyId = 1,
            OwnerId = 9,
            PropertyName = "Sunrise",
            AddressText = "Test address"
        });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3_000_000m });
        context.Rooms.Add(new Rooms
        {
            RoomId = 1,
            PropertyId = 1,
            RoomTypeId = 1,
            RoomCode = "A101",
            Status = RoomStatuses.Occupied
        });
        context.Contracts.Add(new Contracts
        {
            ContractId = 1,
            RoomId = 1,
            CustomerId = 10,
            ProcessedByStaffId = 7,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2027, 10, 31),
            MonthlyRent = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = ContractStatuses.Active
        });
        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            PropertyId = 1,
            ManagerId = 7
        });
        await context.SaveChangesAsync();
    }

    private static Notifications Notification() => new()
    {
        UserId = 10,
        Title = "Contract updated",
        Type = NotificationTypes.Contract,
        IsRead = false
    };

    private static Users User(int userId, int roleId, string name) => new()
    {
        UserId = userId,
        RoleId = roleId,
        FullName = name,
        Email = $"{userId}@example.test",
        PasswordHash = "test",
        IsActive = true
    };
}
