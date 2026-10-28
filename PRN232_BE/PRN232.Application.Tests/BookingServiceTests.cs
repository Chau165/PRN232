using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_creates_pending_booking_with_one_month_deposit_and_payment_window()
    {
        var repository = new FakeBookingRepository { Room = new BookingRoomDto(5, 3_000_000m) };

        var created = await CreateService(repository).CreateAsync(new CreateBookingDto(5), 42);

        var added = Assert.Single(repository.Added);
        Assert.Equal((5, 42, 3_000_000m, "Pending"), (added.RoomId, added.CustomerId, added.DepositAmount, added.Status));
        Assert.Equal(Now.AddMinutes(15).UtcDateTime, added.ExpiryDate);
        Assert.Equal(DateTimeKind.Utc, added.ExpiryDate.Kind);
        Assert.Equal(Now, repository.LastAsOfUtc);
        Assert.Equal(added.BookingId, created.BookingId);
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_room()
    {
        var repository = new FakeBookingRepository();

        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            CreateService(repository).CreateAsync(new CreateBookingDto(5), 42));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_rejects_room_that_is_not_rentable()
    {
        var repository = new FakeBookingRepository { Room = new BookingRoomDto(5, 3_000_000m), RoomIsRentable = false };

        await Assert.ThrowsAsync<BookingRoomUnavailableException>(() =>
            CreateService(repository).CreateAsync(new CreateBookingDto(5), 42));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(RoleNames.Customer, 42, null)]
    [InlineData(RoleNames.Manager, null, 42)]
    [InlineData(RoleNames.Admin, null, null)]
    public async Task GetByIdAsync_scopes_lookup_by_role(string role, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42), OwnerId = 42 };

        var booking = await CreateService(repository).GetByIdAsync(1, 42, role);

        Assert.NotNull(booking);
        Assert.Equal((expectedCustomerId, expectedOwnerId), repository.LastScope);
    }

    [Theory]
    [InlineData(RoleNames.Customer)]
    [InlineData(RoleNames.Manager)]
    public async Task GetByIdAsync_denies_existing_booking_outside_scope(string role)
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 10), OwnerId = 7 };

        await Assert.ThrowsAsync<BookingAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 42, role));
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_missing_booking()
    {
        var booking = await CreateService(new FakeBookingRepository()).GetByIdAsync(1, 42, RoleNames.Customer);

        Assert.Null(booking);
    }

    [Fact]
    public async Task GetByIdAsync_rejects_unknown_role()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };

        await Assert.ThrowsAsync<BookingAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 42, "Guest"));
    }

    private static BookingService CreateService(FakeBookingRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static BookingDto SampleBooking(int customerId) =>
        new(1, 5, "A-101", 1, "Sunrise", customerId, "Customer", 3_000_000m, "Pending", Now.UtcDateTime);

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public BookingRoomDto? Room { get; init; }
        public bool RoomIsRentable { get; init; } = true;
        public BookingDto? Booking { get; set; }
        public int OwnerId { get; init; } = 7;
        public List<Bookings> Added { get; } = [];
        public DateTimeOffset? LastAsOfUtc { get; private set; }
        public (int? CustomerId, int? OwnerId) LastScope { get; private set; }

        public Task<BookingRoomDto?> GetRoomAsync(int roomId) =>
            Task.FromResult(Room?.RoomId == roomId ? Room : null);

        public Task<bool> AddIfRoomRentableAsync(Bookings booking, DateTimeOffset asOfUtc)
        {
            LastAsOfUtc = asOfUtc;
            if (!RoomIsRentable)
            {
                return Task.FromResult(false);
            }

            booking.BookingId = Added.Count + 1;
            Added.Add(booking);
            Booking = new BookingDto(booking.BookingId, booking.RoomId, "A-101", 1, "Sunrise", booking.CustomerId,
                "Customer", booking.DepositAmount, booking.Status, booking.ExpiryDate);
            return Task.FromResult(true);
        }

        public Task<BookingDto?> GetByIdAsync(int bookingId, int? customerId, int? ownerId)
        {
            LastScope = (customerId, ownerId);
            var visible = Booking is not null && Booking.BookingId == bookingId &&
                (customerId is null || Booking.CustomerId == customerId) &&
                (ownerId is null || OwnerId == ownerId);
            return Task.FromResult(visible ? Booking : null);
        }

        public Task<bool> ExistsAsync(int bookingId) => Task.FromResult(Booking?.BookingId == bookingId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
