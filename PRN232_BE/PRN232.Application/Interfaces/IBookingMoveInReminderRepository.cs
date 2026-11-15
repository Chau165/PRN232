using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingMoveInReminderRepository
{
    // Booking đã cọc, chưa có hợp đồng, có ngày dọn vào trong [from, to].
    Task<List<BookingMoveInCandidateReadModel>> GetDepositedBookingsMovingInAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken);

    // Khóa "userId|title" của các thông báo booking đã gửi cho những booking này.
    Task<HashSet<string>> GetSentReminderKeysAsync(
        IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken);

    Task AddNotificationsAsync(IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken);
}

public interface IBookingMoveInReminderService
{
    // Trả về số thông báo đã tạo.
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken);
}
