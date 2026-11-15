using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class BookingMoveInReminderRepository(AppDbContext context) : IBookingMoveInReminderRepository
{
    public async Task<List<BookingMoveInCandidateReadModel>> GetDepositedBookingsMovingInAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatuses.Deposited && booking.Contract == null &&
                              booking.MoveInDate != null && booking.MoveInDate >= from && booking.MoveInDate <= to)
            .OrderBy(booking => booking.BookingId)
            .Select(booking => new
            {
                booking.BookingId,
                booking.CustomerId,
                CustomerName = booking.Customer!.FullName,
                booking.Room!.RoomCode,
                MoveInDate = booking.MoveInDate!.Value,
                booking.Room.Property!.OwnerId,
                ManagerIds = booking.Room.Property.ManagerAssignments
                    .Where(assignment => assignment.Manager!.IsActive &&
                                         assignment.Manager.Role!.RoleName == RoleNames.Manager)
                    .Select(assignment => assignment.ManagerId)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Khu chưa có quản lý thì nhắc chủ trọ.
        return rows.Select(row => new BookingMoveInCandidateReadModel(row.BookingId, row.CustomerId, row.CustomerName,
                row.RoomCode, row.MoveInDate, row.ManagerIds.Count > 0 ? row.ManagerIds : [row.OwnerId]))
            .ToList();
    }

    public async Task<HashSet<string>> GetSentReminderKeysAsync(
        IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken)
    {
        var sent = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.Type == NotificationTypes.Booking &&
                                   notification.RelatedEntityId != null &&
                                   bookingIds.Contains(notification.RelatedEntityId.Value))
            .Select(notification => new { notification.UserId, notification.Title })
            .ToListAsync(cancellationToken);
        return sent.Select(item => ContractExpiryReminderService.Key(item.UserId, item.Title)).ToHashSet();
    }

    public async Task AddNotificationsAsync(IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
    }
}
