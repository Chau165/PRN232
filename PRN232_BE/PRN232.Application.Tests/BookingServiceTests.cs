using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
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

    [Fact]
    public async Task PayAsync_charges_deposit_records_payment_and_notifies_owner()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };
        var gateway = new FakePaymentGateway();

        var paid = await CreateService(repository, gateway).PayAsync(1, new PayBookingDto(PaymentMethods.VNPay), 42);

        Assert.Equal((PaymentMethods.VNPay, 3_000_000m), Assert.Single(gateway.Charges));
        var deposit = Assert.Single(repository.Deposits);
        Assert.Equal((1, Now, Now.AddDays(7).UtcDateTime), (deposit.BookingId, deposit.AsOfUtc, deposit.HoldUntilUtc));
        Assert.Equal(DateTimeKind.Utc, deposit.HoldUntilUtc.Kind);
        Assert.Equal((42, (int?)1, 3_000_000m, PaymentMethods.VNPay, PaymentStatuses.Success),
            (deposit.Payment.UserId, deposit.Payment.BookingId, deposit.Payment.Amount,
                deposit.Payment.PaymentMethod, deposit.Payment.Status));
        Assert.Equal((7, NotificationTypes.Booking, false),
            (deposit.Notification.UserId, deposit.Notification.Type, deposit.Notification.IsRead));
        Assert.Contains("A-101", deposit.Notification.Title);
        Assert.Equal(BookingStatuses.Deposited, paid.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Cash")]
    [InlineData("vnpay")]
    public async Task PayAsync_rejects_unsupported_payment_method(string method)
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };
        var gateway = new FakePaymentGateway();

        await Assert.ThrowsAsync<BookingBusinessRuleException>(() =>
            CreateService(repository, gateway).PayAsync(1, new PayBookingDto(method), 42));
        Assert.Empty(gateway.Charges);
    }

    [Fact]
    public async Task PayAsync_rejects_missing_booking()
    {
        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            CreateService(new FakeBookingRepository()).PayAsync(1, new PayBookingDto(PaymentMethods.MoMo), 42));
    }

    [Fact]
    public async Task PayAsync_rejects_another_customers_booking()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 10) };
        var gateway = new FakePaymentGateway();

        await Assert.ThrowsAsync<BookingAuthorizationException>(() =>
            CreateService(repository, gateway).PayAsync(1, new PayBookingDto(PaymentMethods.MoMo), 42));
        Assert.Empty(gateway.Charges);
    }

    [Theory]
    [InlineData(BookingStatuses.Deposited, 15)]
    [InlineData(BookingStatuses.Cancelled, 15)]
    [InlineData(BookingStatuses.Pending, 0)]
    [InlineData(BookingStatuses.Pending, -1)]
    public async Task PayAsync_rejects_booking_that_is_not_awaiting_payment(string status, int minutesLeft)
    {
        var repository = new FakeBookingRepository
        {
            Booking = SampleBooking(customerId: 42, status, Now.AddMinutes(minutesLeft).UtcDateTime)
        };
        var gateway = new FakePaymentGateway();

        await Assert.ThrowsAsync<BookingStatusConflictException>(() =>
            CreateService(repository, gateway).PayAsync(1, new PayBookingDto(PaymentMethods.MoMo), 42));
        Assert.Empty(gateway.Charges);
        Assert.Empty(repository.Deposits);
    }

    [Fact]
    public async Task PayAsync_does_not_record_deposit_when_gateway_declines()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };

        await Assert.ThrowsAsync<BookingPaymentFailedException>(() =>
            CreateService(repository, new FakePaymentGateway { Succeeds = false })
                .PayAsync(1, new PayBookingDto(PaymentMethods.MoMo), 42));
        Assert.Empty(repository.Deposits);
    }

    [Fact]
    public async Task PayAsync_reports_conflict_when_booking_changed_before_saving()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42), DepositSucceeds = false };

        await Assert.ThrowsAsync<BookingStatusConflictException>(() =>
            CreateService(repository).PayAsync(1, new PayBookingDto(PaymentMethods.MoMo), 42));
    }

    [Theory]
    [InlineData(RoleNames.Customer, null, 42, null)]
    [InlineData(RoleNames.Manager, BookingStatuses.Deposited, null, 42)]
    [InlineData(RoleNames.Admin, BookingStatuses.Cancelled, null, null)]
    public async Task GetAllAsync_scopes_list_by_role_and_passes_status(
        string role, string? status, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };

        await CreateService(repository).GetAllAsync(42, role, status);

        Assert.Equal((expectedCustomerId, expectedOwnerId), repository.LastScope);
        Assert.Equal(status, repository.LastStatus);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("Expired")]
    public async Task GetAllAsync_rejects_unknown_status(string status)
    {
        await Assert.ThrowsAsync<BookingBusinessRuleException>(() =>
            CreateService(new FakeBookingRepository()).GetAllAsync(42, RoleNames.Admin, status));
    }

    [Fact]
    public async Task CancelAsync_lets_customer_cancel_unpaid_booking_without_notification()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42) };

        var cancelled = await CreateService(repository).CancelAsync(1, 42, RoleNames.Customer);

        var cancellation = Assert.Single(repository.Cancellations);
        Assert.Equal((1, BookingStatuses.Pending), (cancellation.BookingId, cancellation.ExpectedStatus));
        Assert.Null(cancellation.Notification);
        Assert.Equal(BookingStatuses.Cancelled, cancelled.Status);
    }

    [Theory]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.Admin)]
    public async Task CancelAsync_lets_staff_cancel_paid_booking_and_notifies_customer(string role)
    {
        var repository = new FakeBookingRepository
        {
            Booking = SampleBooking(customerId: 10, BookingStatuses.Deposited), OwnerId = 42
        };

        await CreateService(repository).CancelAsync(1, 42, role);

        var cancellation = Assert.Single(repository.Cancellations);
        Assert.Equal(BookingStatuses.Deposited, cancellation.ExpectedStatus);
        Assert.NotNull(cancellation.Notification);
        Assert.Equal((10, NotificationTypes.Booking, false),
            (cancellation.Notification.UserId, cancellation.Notification.Type, cancellation.Notification.IsRead));
        Assert.Contains("A-101", cancellation.Notification.Title);
    }

    [Theory]
    [InlineData(RoleNames.Customer, BookingStatuses.Deposited)]
    [InlineData(RoleNames.Customer, BookingStatuses.Cancelled)]
    [InlineData(RoleNames.Admin, BookingStatuses.Cancelled)]
    public async Task CancelAsync_rejects_disallowed_status(string role, string status)
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42, status) };

        await Assert.ThrowsAsync<BookingStatusConflictException>(() =>
            CreateService(repository).CancelAsync(1, 42, role));
        Assert.Empty(repository.Cancellations);
    }

    [Fact]
    public async Task CancelAsync_rejects_missing_or_out_of_scope_booking()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 10) };

        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            CreateService(repository).CancelAsync(99, 42, RoleNames.Customer));
        await Assert.ThrowsAsync<BookingAuthorizationException>(() =>
            CreateService(repository).CancelAsync(1, 42, RoleNames.Customer));
        Assert.Empty(repository.Cancellations);
    }

    [Fact]
    public async Task CancelAsync_reports_conflict_when_status_changed_before_saving()
    {
        var repository = new FakeBookingRepository { Booking = SampleBooking(customerId: 42), CancelSucceeds = false };

        await Assert.ThrowsAsync<BookingStatusConflictException>(() =>
            CreateService(repository).CancelAsync(1, 42, RoleNames.Customer));
    }

    private static BookingService CreateService(FakeBookingRepository repository, FakePaymentGateway? gateway = null) =>
        new(repository, gateway ?? new FakePaymentGateway(), new FixedTimeProvider(Now));

    private static BookingDto SampleBooking(
        int customerId, string status = BookingStatuses.Pending, DateTime? expiryDate = null) =>
        new(1, 5, "A-101", 1, "Sunrise", customerId, "Customer", 3_000_000m, status,
            expiryDate ?? Now.AddMinutes(15).UtcDateTime);

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public BookingRoomDto? Room { get; init; }
        public bool RoomIsRentable { get; init; } = true;
        public bool DepositSucceeds { get; init; } = true;
        public bool CancelSucceeds { get; init; } = true;
        public BookingDto? Booking { get; set; }
        public int OwnerId { get; init; } = 7;
        public List<Bookings> Added { get; } = [];
        public List<(int BookingId, DateTimeOffset AsOfUtc, DateTime HoldUntilUtc, Payments Payment,
            Notifications Notification)> Deposits { get; } = [];
        public List<(int BookingId, string ExpectedStatus, Notifications? Notification)> Cancellations { get; } = [];
        public DateTimeOffset? LastAsOfUtc { get; private set; }
        public (int? CustomerId, int? OwnerId) LastScope { get; private set; }
        public string? LastStatus { get; private set; }

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

        public Task<List<BookingDto>> GetAllAsync(int? customerId, int? ownerId, string? status)
        {
            LastScope = (customerId, ownerId);
            LastStatus = status;
            return Task.FromResult(Booking is null ? new List<BookingDto>() : [Booking]);
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

        public Task<BookingPaymentDto?> GetForPaymentAsync(int bookingId) =>
            Task.FromResult(Booking?.BookingId == bookingId
                ? new BookingPaymentDto(Booking.BookingId, Booking.CustomerId, OwnerId, Booking.RoomCode,
                    Booking.DepositAmount, Booking.Status, Booking.ExpiryDate)
                : null);

        public Task<bool> MarkDepositedAsync(
            int bookingId, DateTimeOffset asOfUtc, DateTime holdUntilUtc, Payments payment, Notifications notification)
        {
            if (!DepositSucceeds)
            {
                return Task.FromResult(false);
            }

            Deposits.Add((bookingId, asOfUtc, holdUntilUtc, payment, notification));
            Booking = Booking! with { Status = BookingStatuses.Deposited, ExpiryDate = holdUntilUtc };
            return Task.FromResult(true);
        }

        public Task<bool> CancelAsync(int bookingId, string expectedStatus, Notifications? customerNotification)
        {
            if (!CancelSucceeds)
            {
                return Task.FromResult(false);
            }

            Cancellations.Add((bookingId, expectedStatus, customerNotification));
            Booking = Booking! with { Status = BookingStatuses.Cancelled };
            return Task.FromResult(true);
        }
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public bool Succeeds { get; init; } = true;
        public List<(string Method, decimal Amount)> Charges { get; } = [];

        public Task<bool> ChargeAsync(string paymentMethod, decimal amount)
        {
            Charges.Add((paymentMethod, amount));
            return Task.FromResult(Succeeds);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
