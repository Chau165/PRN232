using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.4: reminders before and after a contract's end date, sent once per stage and recipient.
// The contract stays Active: the tenant still lives there until extension or checkout.
public sealed class ContractExpiryReminderTests : IAsyncLifetime
{
    // 15/11/2026 10:00 in Vietnam.
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 15);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _context.Database.EnsureCreatedAsync();
        _context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        _context.Users.AddRange(
            User(7, 2, isActive: true),
            User(8, 2, isActive: false),
            User(9, 3, isActive: true),
            User(10, 1, isActive: true));
        _context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Hoa Sen" });
        _context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio" });
        _context.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 1, RoomTypeId = 1, MonthlyRent = 3_000_000m, IsActive = true });
        _context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Contract_ending_within_30_days_notifies_tenant_and_active_managers_once()
    {
        await AddContractAsync(1, "A101", Today.AddDays(20));

        Assert.Equal(2, await RunAsync());
        Assert.Equal(0, await RunAsync());

        var notifications = await _context.Notifications.AsNoTracking().OrderBy(n => n.UserId).ToListAsync();
        Assert.Equal([7, 10], notifications.Select(n => n.UserId));
        Assert.All(notifications, notification =>
        {
            Assert.Equal(NotificationTypes.Contract, notification.Type);
            Assert.Equal(1, notification.RelatedEntityId);
            Assert.Equal("Hợp đồng phòng A101 sẽ hết hạn ngày 05/12/2026 (còn dưới 30 ngày)", notification.Title);
        });
    }

    [Fact]
    public async Task Contract_ending_within_7_days_gets_only_the_urgent_reminder()
    {
        await AddContractAsync(1, "A101", Today.AddDays(5));

        await RunAsync();

        var titles = await _context.Notifications.Select(n => n.Title).Distinct().ToListAsync();
        Assert.Equal(["Hợp đồng phòng A101 sẽ hết hạn ngày 20/11/2026 (còn dưới 7 ngày)"], titles);
    }

    [Fact]
    public async Task Overdue_active_contract_asks_for_extension_or_checkout()
    {
        await AddContractAsync(1, "A101", Today.AddDays(-3));

        await RunAsync();

        var titles = await _context.Notifications.Select(n => n.Title).Distinct().ToListAsync();
        Assert.Equal(["Hợp đồng phòng A101 đã hết hạn ngày 12/11/2026 nhưng chưa gia hạn hoặc trả phòng"], titles);
        Assert.Equal(ContractStatuses.Active, (await _context.Contracts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Far_away_or_terminated_contracts_are_ignored()
    {
        await AddContractAsync(1, "A101", Today.AddDays(31));
        await AddContractAsync(2, "A102", Today.AddDays(3), ContractStatuses.Terminated);

        Assert.Equal(0, await RunAsync());
    }

    [Fact]
    public async Task Extending_a_contract_restarts_its_reminders()
    {
        await AddContractAsync(1, "A101", Today.AddDays(5));
        await RunAsync();

        var contract = await _context.Contracts.SingleAsync();
        contract.EndDate = Today.AddDays(25);
        await _context.SaveChangesAsync();

        Assert.Equal(2, await RunAsync());
    }

    [Fact]
    public void Reminder_service_is_registered_for_the_worker()
    {
        using var factory = new MaintenanceApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.IsType<ContractExpiryReminderService>(
            scope.ServiceProvider.GetRequiredService<PRN232.Application.Interfaces.IContractExpiryReminderService>());
    }

    private Task<int> RunAsync() =>
        new ContractExpiryReminderService(new ContractExpiryReminderRepository(_context), new FixedTimeProvider(Now))
            .SendDueRemindersAsync(CancellationToken.None);

    private async Task AddContractAsync(int id, string roomCode, DateOnly endDate, string status = ContractStatuses.Active)
    {
        _context.Rooms.Add(new Rooms { RoomId = id, PropertyId = 1, RoomTypeId = 1, RoomCode = roomCode, AreaSqm = 20 });
        _context.Contracts.Add(new Contracts
        {
            ContractId = id,
            CustomerId = 10,
            RoomId = id,
            StartDate = endDate.AddYears(-1),
            EndDate = endDate,
            MonthlyRent = 3_000_000m,
            Status = status
        });
        await _context.SaveChangesAsync();
    }

    private static Users User(int id, int roleId, bool isActive) => new()
    {
        UserId = id,
        RoleId = roleId,
        FullName = $"User {id}",
        Email = $"user{id}@example.test",
        PhoneNumber = "0901234567",
        IsActive = isActive
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
