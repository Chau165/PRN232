using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.2c: remind the customer and staff of the move-in date, and the customer on the last held day.
public sealed class BookingMoveInReminderTests
{
    // 2026-11-20 03:00 UTC is 10:00 on 20/11 in Vietnam.
    private static readonly DateTimeOffset Nov20 = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);

    private static BookingMoveInCandidateReadModel Booking(DateOnly moveIn) =>
        new(12, 10, "Nguyễn Văn A", "A101", moveIn, [7, 8]);

    [Fact]
    public async Task Move_in_day_reminds_the_customer_and_every_manager_once()
    {
        var repository = new FakeRepository { Bookings = [Booking(new DateOnly(2026, 11, 20))] };
        var service = new BookingMoveInReminderService(repository, new FixedTimeProvider(Nov20));

        Assert.Equal(3, await service.SendDueRemindersAsync(CancellationToken.None));
        Assert.Equal(0, await service.SendDueRemindersAsync(CancellationToken.None));

        Assert.Equal((new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 20)), repository.LastRange);
        var customer = Assert.Single(repository.Added, n => n.UserId == 10);
        Assert.Equal("Ngày dọn vào phòng A101: 20/11/2026", customer.Title);
        Assert.Contains("đến hết ngày 23/11/2026", customer.Body);
        Assert.Equal((NotificationTypes.Booking, (int?)12), (customer.Type, customer.RelatedEntityId));
        Assert.Equal(["Khách Nguyễn Văn A dọn vào phòng A101 ngày 20/11/2026"],
            repository.Added.Where(n => n.UserId is 7 or 8).Select(n => n.Title).Distinct());
    }

    [Fact]
    public async Task Last_held_day_warns_the_customer_that_the_deposit_can_be_lost()
    {
        // Moved in 17/11: today (20/11) is the last held day. The move-in reminder was already sent.
        var repository = new FakeRepository { Bookings = [Booking(new DateOnly(2026, 11, 17))] };
        foreach (var (userId, title) in new[] { (10, "Ngày dọn vào phòng A101: 17/11/2026"),
                     (7, "Khách Nguyễn Văn A dọn vào phòng A101 ngày 17/11/2026"),
                     (8, "Khách Nguyễn Văn A dọn vào phòng A101 ngày 17/11/2026") })
        {
            repository.Sent.Add(ContractExpiryReminderService.Key(userId, title));
        }

        Assert.Equal(1, await new BookingMoveInReminderService(repository, new FixedTimeProvider(Nov20))
            .SendDueRemindersAsync(CancellationToken.None));

        var warning = Assert.Single(repository.Added);
        Assert.Equal((10, "Phòng A101 chỉ được giữ đến hết ngày 20/11/2026"), (warning.UserId, warning.Title));
        Assert.Contains("tiền cọc không được hoàn", warning.Body);
    }

    [Fact]
    public async Task A_missed_move_in_day_is_still_reminded_later_in_the_hold()
    {
        var repository = new FakeRepository { Bookings = [Booking(new DateOnly(2026, 11, 18))] };

        await new BookingMoveInReminderService(repository, new FixedTimeProvider(Nov20)).SendDueRemindersAsync(CancellationToken.None);

        Assert.Contains(repository.Added, n => n.UserId == 10 && n.Title == "Ngày dọn vào phòng A101: 18/11/2026");
        Assert.DoesNotContain(repository.Added, n => n.Title.StartsWith("Phòng A101 chỉ được giữ", StringComparison.Ordinal));
    }

    private sealed class FakeRepository : IBookingMoveInReminderRepository
    {
        public List<BookingMoveInCandidateReadModel> Bookings { get; init; } = [];
        public HashSet<string> Sent { get; } = [];
        public List<Notifications> Added { get; } = [];
        public (DateOnly From, DateOnly To) LastRange { get; private set; }

        public Task<List<BookingMoveInCandidateReadModel>> GetDepositedBookingsMovingInAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            LastRange = (from, to);
            return Task.FromResult(Bookings.Where(b => b.MoveInDate >= from && b.MoveInDate <= to).ToList());
        }

        public Task<HashSet<string>> GetSentReminderKeysAsync(IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken) =>
            Task.FromResult(new HashSet<string>(Sent));

        public Task AddNotificationsAsync(IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
        {
            Added.AddRange(notifications);
            foreach (var notification in notifications)
            {
                Sent.Add(ContractExpiryReminderService.Key(notification.UserId, notification.Title));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
