using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;

namespace PRN232.Application.Tests;

public class RoomTypeBookingTests
{
    private static DateOnly Today => BookingRulesToday();
    private static DateOnly BookingRulesToday() => PRN232.Application.Validation.BookingRules.Today(TimeProvider.System);
    private sealed class Gateway : IPaymentGateway { public Task<bool> ChargeAsync(string method, decimal amount) => Task.FromResult(true); }
    private static BookingService Service(AppDbContext db) => new(new BookingRepository(db), new Gateway(), TimeProvider.System);
    private static CreateBookingDto TypeRequest(int occupants = 1, int? viewingId = null) => new(
        ViewingId: viewingId, MoveInDate: Today, OccupantCount: occupants, PropertyId: 1, RoomTypeId: 1);

    private static async Task PrepareAsync(AppDbContext db)
    {
        await db.Rooms.ExecuteUpdateAsync(setters => setters.SetProperty(room => room.Status, "Available").SetProperty(room => room.MaxOccupants, 2));
        db.Rooms.Add(new Rooms { RoomId = 3, PropertyId = 1, RoomTypeId = 1, RoomCode = "A103", Status = "Available", MaxOccupants = 2 });
        foreach (var id in new[] { 30, 40 }) db.Users.Add(new Users { UserId = id, RoleId = 1, FullName = "Synthetic " + id,
            Email = id + "@fixture.invalid", PasswordHash = "test-only", PhoneNumber = "0000000000", IsActive = true });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public async Task Approved_viewing_with_or_without_viewed_room_allows_type_deposit_without_allocation(int? viewedRoomId)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await PrepareAsync(db);
        db.ViewingSchedules.Add(new ViewingSchedules { ViewingId = 1, PropertyId = 1, RoomTypeId = 1,
            RoomId = viewedRoomId, CustomerId = 10, AssignedTo = 7, Status = "Approved" });
        await db.SaveChangesAsync();
        // Another customer's valid physical hold must never be inherited from this viewing.
        await Service(db).CreateAsync(new CreateBookingDto(2), 20);
        var created = await Service(db).CreateAsync(TypeRequest(viewingId: 1), 10);
        await db.PropertyRoomTypes.ExecuteUpdateAsync(setters => setters.SetProperty(type => type.MonthlyRent, 4200000m));
        var paid = await Service(db).PayAsync(created.BookingId, new PayBookingDto("MoMo"), 10);
        Assert.Null(created.RoomId); Assert.Null(paid.RoomId); Assert.Equal("", paid.RoomCode);
        Assert.Equal((1, 1, 3000000m, 3000000m), (paid.PropertyId, paid.RoomTypeId, paid.AgreedMonthlyRent, paid.DepositAmount));
        await LoginAsync(client, "manager@example.test");
        var options = await client.GetFromJsonAsync<List<EligibleContractRoomDto>>($"/api/bookings/{paid.BookingId}/eligible-contract-rooms");
        Assert.Equal(new[] { 1, 3 }, options!.Select(room => room.RoomId));
        var response = await client.PostAsJsonAsync("/api/contracts", Contract(paid.BookingId, 3));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contract = (await response.Content.ReadFromJsonAsync<ContractDto>())!;
        var booking = await db.Bookings.AsNoTracking().SingleAsync(item => item.BookingId == paid.BookingId);
        Assert.Equal(3, contract.RoomId); Assert.Equal(3, booking.RoomId);
        Assert.Equal(3000000m, contract.MonthlyRent); Assert.Equal("Occupied", (await db.Rooms.AsNoTracking().SingleAsync(room => room.RoomId == 3)).Status);
        var audit = await db.Set<AuditLogs>().SingleAsync(log => log.Action == "ConfirmContractRoomSelection");
        Assert.Equal(7, audit.UserId); Assert.Contains("\"CustomerAgreed\":true", audit.NewValues); Assert.Contains("\"RoomId\":3", audit.NewValues);
        Assert.Equal(viewedRoomId, (await db.ViewingSchedules.AsNoTracking().SingleAsync()).RoomId);
    }

    private static CreateContractDto Contract(int bookingId, int? roomId, bool acknowledged = true) => new(bookingId,
        Today, Today.AddMonths(6), "079201000001", [], 0, 0m, true, RoomId: roomId, CustomerRoomSelectionAcknowledged: acknowledged);

    [Fact]
    public async Task Three_rooms_hold_at_most_three_reservations_with_mixed_physical_and_type_paths()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await PrepareAsync(db);
        await Service(db).CreateAsync(new CreateBookingDto(1), 10);
        var second = await Service(db).CreateAsync(TypeRequest(), 20);
        await Service(db).PayAsync(second.BookingId, new PayBookingDto("MoMo"), 20);
        await Service(db).CreateAsync(TypeRequest(), 30);
        await Assert.ThrowsAsync<BookingRoomUnavailableException>(() => Service(db).CreateAsync(TypeRequest(), 40));
        await Assert.ThrowsAsync<BookingRoomUnavailableException>(() => Service(db).CreateAsync(new CreateBookingDto(3), 40));
        Assert.Equal(3, await db.Bookings.CountAsync());
        Assert.Equal(2, await db.Bookings.CountAsync(booking => booking.RoomId == null));
    }

    [Fact]
    public async Task Management_type_availability_subtracts_unassigned_reservations_once()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await PrepareAsync(db);
        await Service(db).CreateAsync(TypeRequest(), 10);
        // The full availability aggregate (including decimal MIN) is verified on PostgreSQL.
        var type = await new PropertyRoomTypeRepository(db).GetByIdAsync(1, 1, DateTimeOffset.UtcNow);
        Assert.Equal(2, type!.AvailableRoomCount);
    }

    [Theory]
    [InlineData("Pending", false, true)]
    [InlineData("Pending", true, false)]
    [InlineData("Deposited", false, false)]
    [InlineData("Cancelled", false, true)]
    public async Task Expiry_and_cancellation_release_only_resolved_unpaid_capacity(string status, bool uncertainCheckout, bool canBook)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var held = await Service(db).CreateAsync(TypeRequest(), 10);
        await db.Bookings.Where(booking => booking.BookingId == held.BookingId).ExecuteUpdateAsync(setters =>
            setters.SetProperty(booking => booking.Status, status).SetProperty(booking => booking.ExpiryDate, DateTime.UtcNow.AddMinutes(-1)));
        if (uncertainCheckout) { db.PaymentRequests.Add(new PaymentRequests { BookingId = held.BookingId, CustomerId = 10,
            Amount = held.DepositAmount, OrderCode = 20001, Status = "RequiresReview", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(); }
        if (canBook) Assert.Null((await Service(db).CreateAsync(TypeRequest(), 20)).RoomId);
        else await Assert.ThrowsAsync<BookingRoomUnavailableException>(() => Service(db).CreateAsync(TypeRequest(), 20));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public async Task Wrong_viewing_property_or_type_is_rejected(int propertyId, int typeId)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        using var response = await client.PostAsJsonAsync("/api/bookings", new { propertyId, roomTypeId = typeId });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_type_and_excessive_occupancy_are_rejected_without_a_booking()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.ThrowsAsync<BookingOccupancyExceededException>(() => Service(db).CreateAsync(TypeRequest(3), 10));
        await db.PropertyRoomTypes.ExecuteUpdateAsync(setters => setters.SetProperty(type => type.IsActive, false));
        await Assert.ThrowsAsync<BookingNotFoundException>(() => Service(db).CreateAsync(TypeRequest(), 10));
        Assert.Empty(await db.Bookings.ToListAsync());
    }

    [Theory]
    [InlineData(null, true, HttpStatusCode.BadRequest)]
    [InlineData(1, false, HttpStatusCode.BadRequest)]
    [InlineData(2, true, HttpStatusCode.Conflict)] // occupied
    [InlineData(999, true, HttpStatusCode.Conflict)]
    public async Task Missing_consent_or_invalid_allocation_never_assigns_booking(int? roomId, bool consent, HttpStatusCode expected)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await Service(db).CreateAsync(TypeRequest(), 10); await Service(db).PayAsync(booking.BookingId, new PayBookingDto("MoMo"), 10);
        await LoginAsync(client, "manager@example.test");
        var response = await client.PostAsJsonAsync("/api/contracts", Contract(booking.BookingId, roomId, consent));
        Assert.Equal(expected, response.StatusCode);
        Assert.Null((await db.Bookings.AsNoTracking().SingleAsync()).RoomId); Assert.Empty(await db.Contracts.ToListAsync());
        Assert.Empty(await db.Set<AuditLogs>().Where(log => log.Action == "ConfirmContractRoomSelection").ToListAsync());
    }

    [Theory]
    [InlineData("property")]
    [InlineData("type")]
    [InlineData("maintenance")]
    [InlineData("capacity")]
    [InlineData("hold")]
    public async Task Selected_room_is_revalidated_against_location_capacity_status_and_other_holds(string condition)
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await PrepareAsync(db);
        var booking = await Service(db).CreateAsync(TypeRequest(2), 10);
        await Service(db).PayAsync(booking.BookingId, new PayBookingDto("MoMo"), 10);
        if (condition == "property") {
            db.Properties.Add(new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Other property" });
            db.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 2, RoomTypeId = 1, MonthlyRent = 9000000m });
            await db.SaveChangesAsync();
            await db.Rooms.Where(room => room.RoomId == 3).ExecuteUpdateAsync(setters => setters.SetProperty(room => room.PropertyId, 2));
        }
        if (condition == "type") {
            db.RoomTypes.Add(new RoomTypes { RoomTypeId = 2, TypeName = "Other type" });
            db.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 1, RoomTypeId = 2, MonthlyRent = 9000000m });
            await db.SaveChangesAsync();
            await db.Rooms.Where(room => room.RoomId == 3).ExecuteUpdateAsync(setters => setters.SetProperty(room => room.RoomTypeId, 2));
        }
        if (condition == "maintenance") await db.Rooms.Where(room => room.RoomId == 3).ExecuteUpdateAsync(setters => setters.SetProperty(room => room.Status, "Maintenance"));
        if (condition == "capacity") await db.Rooms.Where(room => room.RoomId == 3).ExecuteUpdateAsync(setters => setters.SetProperty(room => room.MaxOccupants, 1));
        if (condition == "hold") await Service(db).CreateAsync(new CreateBookingDto(3), 20);
        await LoginAsync(client, "manager@example.test");
        var response = await client.PostAsJsonAsync("/api/contracts", Contract(booking.BookingId, 3) with { Occupants = [new("Other occupant", "079201000002")] });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null((await db.Bookings.AsNoTracking().SingleAsync(item => item.BookingId == booking.BookingId)).RoomId);
    }

    [Fact]
    public async Task Unassigned_booking_manager_scope_and_legacy_room_switch_are_enforced()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await PrepareAsync(db);
        var typed = await Service(db).CreateAsync(TypeRequest(), 10); await Service(db).PayAsync(typed.BookingId, new PayBookingDto("MoMo"), 10);
        await LoginAsync(client, "manager-b@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/bookings/{typed.BookingId}/eligible-contract-rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/contracts", Contract(typed.BookingId, 3))).StatusCode);
        var legacy = await Service(db).CreateAsync(new CreateBookingDto(1), 20); await Service(db).PayAsync(legacy.BookingId, new PayBookingDto("MoMo"), 20);
        await LoginAsync(client, "manager@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/contracts", Contract(legacy.BookingId, 3))).StatusCode);
        Assert.Equal(1, (await db.Bookings.AsNoTracking().SingleAsync(item => item.BookingId == legacy.BookingId)).RoomId);
    }

    [Fact]
    public async Task Failure_after_assignment_rolls_back_room_contract_consent_and_notification()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await Service(db).CreateAsync(TypeRequest(), 10); await Service(db).PayAsync(booking.BookingId, new PayBookingDto("MoMo"), 10);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_contract BEFORE INSERT ON Contracts BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        var service = scope.ServiceProvider.GetRequiredService<IContractService>();
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateFromBookingAsync(Contract(booking.BookingId, 1), 7, "Manager"));
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await verify.Bookings.SingleAsync()).RoomId); Assert.Empty(await verify.Contracts.ToListAsync());
        Assert.Equal("Available", (await verify.Rooms.SingleAsync(room => room.RoomId == 1)).Status);
        Assert.Empty(await verify.Set<AuditLogs>().Where(log => log.Action == "ConfirmContractRoomSelection").ToListAsync());
        Assert.Empty(await verify.Notifications.Where(note => note.Type == "Contract").ToListAsync());
    }

    [Fact]
    public async Task Physical_availability_change_retains_deposited_entitlement_and_blocks_new_checkout()
    {
        using var factory = new BookingsApiFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await PrepareAsync(db);
        var booking = await Service(db).CreateAsync(TypeRequest(), 10); await Service(db).PayAsync(booking.BookingId, new PayBookingDto("MoMo"), 10);
        await db.Rooms.ExecuteUpdateAsync(setters => setters.SetProperty(room => room.Status, "Maintenance"));
        Assert.Empty(await new BookingRepository(db).GetEligibleContractRoomsAsync(booking.BookingId, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<BookingRoomUnavailableException>(() => Service(db).CreateAsync(TypeRequest(), 20));
        var preserved = await db.Bookings.AsNoTracking().SingleAsync(); Assert.Equal("Deposited", preserved.Status); Assert.Null(preserved.RoomId);
    }

    [Fact]
    public void Capacity_matching_protects_large_parties_and_does_not_allocate_rooms()
    {
        Assert.True(RoomInventoryPolicy.CanFit([4, 1], [4, 1]));
        Assert.False(RoomInventoryPolicy.CanFit([4, 1], [4, 2]));
        Assert.False(RoomInventoryPolicy.CanFit([1, 1], [1, 1, 1]));
    }
}
