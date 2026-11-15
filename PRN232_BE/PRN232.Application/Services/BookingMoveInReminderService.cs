using System.Globalization;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Rebuild 3.2c (PLAN.md D3): nhắc khách và quản lý về ngày dọn vào, và nhắc khách vào NGÀY CUỐI giữ phòng.
// Hạn giữ phòng phải được khách biết rõ (điều khoản mất cọc chỉ công bằng khi khách được báo trước).
// Tiêu đề ghi ngày cụ thể (không ghi "hôm nay") nên lần chạy trễ vẫn gửi bù đúng nội dung; gửi trùng được
// chặn bằng khóa người nhận + tiêu đề, như ContractExpiryReminderService.
public sealed class BookingMoveInReminderService(
    IBookingMoveInReminderRepository repository,
    TimeProvider clock,
    INotificationRealtimePublisher? realtimePublisher = null) : IBookingMoveInReminderService
{
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var today = BookingRules.Today(clock);
        // Đang trong thời hạn giữ phòng: ngày dọn vào <= hôm nay <= ngày dọn vào + ân hạn.
        var bookings = await repository.GetDepositedBookingsMovingInAsync(
            today.AddDays(-DepositHoldPolicy.MoveInGraceDays), today, cancellationToken);
        if (bookings.Count == 0)
        {
            return 0;
        }

        var sent = await repository.GetSentReminderKeysAsync(bookings.Select(b => b.BookingId).ToArray(), cancellationToken);
        var created = new List<Notifications>();
        foreach (var booking in bookings)
        {
            foreach (var (userId, title, body) in Reminders(booking, today))
            {
                if (sent.Add(ContractExpiryReminderService.Key(userId, title)))
                {
                    created.Add(new Notifications
                    {
                        UserId = userId,
                        Title = title,
                        Body = body,
                        Type = NotificationTypes.Booking,
                        RelatedEntityId = booking.BookingId,
                        IsRead = false
                    });
                }
            }
        }

        if (created.Count == 0)
        {
            return 0;
        }
        await repository.AddNotificationsAsync(created, cancellationToken);
        if (realtimePublisher is not null)
        {
            await realtimePublisher.PublishCreatedAsync(created);
        }
        return created.Count;
    }

    private static IEnumerable<(int UserId, string Title, string Body)> Reminders(
        BookingMoveInCandidateReadModel booking, DateOnly today)
    {
        var moveIn = Format(booking.MoveInDate);
        var lastDay = booking.MoveInDate.AddDays(DepositHoldPolicy.MoveInGraceDays);

        yield return (booking.CustomerId,
            $"Ngày dọn vào phòng {booking.RoomCode}: {moveIn}",
            $"Phòng được giữ cho bạn đến hết ngày {Format(lastDay)}. Liên hệ quản lý để ký hợp đồng và nhận phòng.");
        foreach (var staffId in booking.StaffIds)
        {
            yield return (staffId,
                $"Khách {booking.CustomerName} dọn vào phòng {booking.RoomCode} ngày {moveIn}",
                "Chuẩn bị hợp đồng, kiểm kê thiết bị và chốt chỉ số điện nước khi khách nhận phòng.");
        }

        if (today == lastDay)
        {
            yield return (booking.CustomerId,
                $"Phòng {booking.RoomCode} chỉ được giữ đến hết ngày {Format(lastDay)}",
                "Nếu hết ngày này bạn chưa nhận phòng, quản lý có thể mở phòng cho người khác thuê và tiền cọc không được hoàn. Liên hệ quản lý nếu cần thêm thời gian.");
        }
    }

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
