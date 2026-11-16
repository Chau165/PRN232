using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Repositories;
using PRN232.Infrastructure.Security;
using Xunit;

namespace PRN232.Application.Tests;

public class RoomTypeBookingPostgreSqlTests
{
    private sealed class Gateway : IPaymentGateway { public Task<bool> ChargeAsync(string method, decimal amount) => Task.FromResult(true); }
    private static BookingService Service(PRN232.Infrastructure.Data.AppDbContext db) => new(new BookingRepository(db), new Gateway(), TimeProvider.System);
    private static DateOnly Today => PRN232.Application.Validation.BookingRules.Today(TimeProvider.System);
    private static CreateBookingDto Request => new(PropertyId: 1, RoomTypeId: 1, MoveInDate: Today, OccupantCount: 1);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(25);
    private static HmacCccdHasher Hasher => new(Encoding.UTF8.GetBytes("room-type-integration-test-key-32-bytes-long"));

    [ScopeBPostgreSqlFact]
    public async Task Availability_aggregates_physical_rooms_minus_type_holds_on_postgresql()
    {
        await using var fixture = await ScopeBReviewPostgres.CreateAsync(); await using var db = fixture.Context();
        db.Rooms.Add(Room(2)); await db.SaveChangesAsync();
        await Service(db).CreateAsync(Request, 10);
        var availability = Assert.Single(await new RoomRepository(db).GetPropertyAvailabilityAsync(DateTimeOffset.UtcNow));
        Assert.Equal(1, availability.AvailableRoomCount); Assert.Equal(700m, availability.StartingPrice);
        var type = await new PropertyRoomTypeRepository(db).GetByIdAsync(1, 1, DateTimeOffset.UtcNow);
        Assert.Equal(1, type!.AvailableRoomCount);
    }

    [ScopeBPostgreSqlFact]
    public async Task Four_concurrent_customers_for_three_rooms_commit_only_three_type_reservations()
    {
        await using var fixture = await ScopeBReviewPostgres.CreateAsync();
        await using (var seed = fixture.Context())
        {
            seed.Rooms.AddRange(Room(2), Room(3));
            seed.Users.AddRange(User(30), User(40)); await seed.SaveChangesAsync();
        }
        var attempts = new[] { 10, 20, 30, 40 }.Select(async customer =>
        {
            await using var db = fixture.Context();
            try { return (Booking: await Service(db).CreateAsync(Request, customer), Error: (Exception?)null); }
            catch (Exception exception) { return (Booking: (BookingDto?)null, Error: exception); }
        });
        var results = await Task.WhenAll(attempts).WaitAsync(Deadline);
        Assert.Equal(3, results.Count(result => result.Booking != null));
        Assert.IsType<BookingRoomUnavailableException>(Assert.Single(results, result => result.Error != null).Error);
        await using var verify = fixture.Context(); Assert.Equal(3, await verify.Bookings.CountAsync());
        Assert.All(await verify.Bookings.ToListAsync(), booking => Assert.Null(booking.RoomId));
    }

    [ScopeBPostgreSqlFact]
    public async Task Two_managers_allocating_same_room_serialize_and_only_one_contract_commits()
    {
        await using var fixture = await ScopeBReviewPostgres.CreateAsync();
        int firstBooking, secondBooking;
        await using (var seed = fixture.Context())
        {
            seed.Rooms.Add(Room(2));
            seed.Users.Add(new Users { UserId = 8, RoleId = 2, IsActive = true, FullName = "Other Manager", Email = "manager8@fixture.invalid", PasswordHash = "test-only" });
            seed.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 8 });
            await seed.SaveChangesAsync();
            firstBooking = (await Service(seed).CreateAsync(Request, 10)).BookingId;
            await Service(seed).PayAsync(firstBooking, new PayBookingDto("MoMo"), 10);
            secondBooking = (await Service(seed).CreateAsync(Request, 20)).BookingId;
            await Service(seed).PayAsync(secondBooking, new PayBookingDto("MoMo"), 20);
        }
        var gate = new ScopeBSqlPause("INSERT INTO \"Contracts\"");
        await using var first = fixture.Context(gate); await using var second = fixture.Context();
        var winner = Attempt(() => new ContractService(new ContractRepository(first, Hasher), TimeProvider.System)
            .CreateFromBookingAsync(Contract(firstBooking, "079201000001"), 7, "Manager"));
        await gate.Entered.WaitAsync(Deadline);
        var loser = Attempt(() => new ContractService(new ContractRepository(second, Hasher), TimeProvider.System)
            .CreateFromBookingAsync(Contract(secondBooking, "079201000002"), 8, "Manager"));
        try { await fixture.AssertBlockedByAsync(first); } finally { gate.Release(); }
        Assert.Null(await winner.WaitAsync(Deadline)); Assert.IsType<ContractConflictException>(await loser.WaitAsync(Deadline));
        await using var verify = fixture.Context(); var contract = await verify.Contracts.SingleAsync();
        Assert.Equal(1, contract.RoomId); Assert.Equal(1, (await verify.Bookings.SingleAsync(booking => booking.BookingId == firstBooking)).RoomId);
        Assert.Null((await verify.Bookings.SingleAsync(booking => booking.BookingId == secondBooking)).RoomId);
        Assert.Equal(1, await verify.Set<AuditLogs>().CountAsync(log => log.Action == "ConfirmContractRoomSelection"));
    }

    [ScopeBPostgreSqlFact]
    public async Task Upgrade_backfills_location_only_and_downgrade_refuses_unassigned_bookings()
    {
        await using var fixture = await ScopeBReviewPostgres.CreateAsync(); await using var db = fixture.Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261115174143_AddIdentityDocumentReviewNote");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Bookings" ("BookingId", "RoomId", "CustomerId", "DepositAmount", "AgreedMonthlyRent", "DepositAppliesToFirstMonth", "Status", "ExpiryDate")
            VALUES (24, 1, 10, 5500000, NULL, true, 'Deposited', CURRENT_TIMESTAMP);
            INSERT INTO "Payments" ("BookingId", "UserId", "Amount", "PaymentMethod", "Status")
            VALUES (24, 10, 5500000, 'MoMo', 'Success');
            """);
        await migrator.MigrateAsync();
        var historical = await db.Bookings.AsNoTracking().SingleAsync();
        Assert.Equal((24, 1, 1, 1, 5500000m), (historical.BookingId, historical.RoomId, historical.PropertyId, historical.RoomTypeId, historical.DepositAmount));
        Assert.Null(historical.AgreedMonthlyRent); Assert.Equal(5500000m, (await db.Payments.SingleAsync()).Amount);
        // Historical unknown rent blocks contract creation; the migration never fabricates it from the payment.
        await Assert.ThrowsAsync<ContractConflictException>(() => new ContractService(new ContractRepository(db, Hasher), TimeProvider.System)
            .CreateFromBookingAsync(Contract(24, "079201000001"), 7, "Manager"));
        db.Rooms.Add(Room(2)); await db.SaveChangesAsync();
        var unassigned = await Service(db).CreateAsync(Request, 20); Assert.Null(unassigned.RoomId);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync("20261115174143_AddIdentityDocumentReviewNote"));
        Assert.Contains("Cannot downgrade", error.MessageText);
        Assert.Null((await db.Bookings.AsNoTracking().SingleAsync(booking => booking.BookingId == unassigned.BookingId)).RoomId);
        Assert.Equal(5500000m, (await db.Payments.SingleAsync()).Amount);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<Exception?> Attempt(Func<Task<ContractDto>> action)
    { try { await action(); return null; } catch (Exception error) { return error; } }
    private static CreateContractDto Contract(int id, string identity) => new(id, Today, Today.AddYears(1), identity, [], 0, 0m, true,
        RoomId: 1, CustomerRoomSelectionAcknowledged: true);
    private static Rooms Room(int id) => new() { RoomId = id, PropertyId = 1, RoomTypeId = 1, RoomCode = "A10" + id, Status = "Available" };
    private static Users User(int id) => new() { UserId = id, RoleId = 1, FullName = "Synthetic " + id, Email = id + "@fixture.invalid", PasswordHash = "test-only", IsActive = true };
}
