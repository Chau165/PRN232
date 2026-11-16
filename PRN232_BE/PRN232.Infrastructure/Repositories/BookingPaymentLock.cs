using Microsoft.EntityFrameworkCore;
using PRN232.Infrastructure.Data;
using PRN232.Application.Policies;

namespace PRN232.Infrastructure.Repositories;

internal static class BookingPaymentLock
{
    // SQLite test/local writes share a gate; PostgreSQL correctness uses DB locks and constraints.
    internal static readonly SemaphoreSlim RefundWriteGate = new(1, 1);
    // Shared with reservation, settlement, refund and allocation: type -> rooms -> booking.
    internal static async Task LockAsync(AppDbContext context, int bookingId, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsNpgsql()) return;
        var location = await context.Bookings.AsNoTracking().Where(item => item.BookingId == bookingId)
            .Select(item => new { item.PropertyId, item.RoomTypeId }).SingleOrDefaultAsync(cancellationToken);
        if (location is null) return;
        await RoomInventoryQueries.LockAsync(context, location.PropertyId, location.RoomTypeId, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {bookingId} FOR UPDATE", cancellationToken);
    }

    internal static async Task<bool> RoomCanAcceptDepositAsync(AppDbContext context, int? roomId, int bookingId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var booking = await context.Bookings.AsNoTracking().SingleAsync(item => item.BookingId == bookingId, cancellationToken);
        if (roomId is null && booking.AgreedMonthlyRent is not > 0m) return false;
        return await RoomInventoryQueries.CanReserveAsync(context, booking.PropertyId, booking.RoomTypeId,
            booking.OccupantCount ?? 1, roomId, bookingId,
            new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)), cancellationToken);
    }
}
