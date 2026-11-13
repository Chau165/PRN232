using Microsoft.EntityFrameworkCore;
using PRN232.Infrastructure.Data;
using PRN232.Application.Policies;

namespace PRN232.Infrastructure.Repositories;

internal static class BookingPaymentLock
{
    // SQLite test/local writes share a gate; PostgreSQL correctness uses DB locks and constraints.
    internal static readonly SemaphoreSlim RefundWriteGate = new(1, 1);
    // Shared with room reservation and contract creation: room first, then booking.
    internal static async Task LockAsync(AppDbContext context, int bookingId, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsNpgsql()) return;
        var roomId = await context.Bookings.Where(item => item.BookingId == bookingId)
            .Select(item => (int?)item.RoomId).SingleOrDefaultAsync(cancellationToken);
        if (!roomId.HasValue) return;
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {roomId.Value} FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {bookingId} FOR UPDATE", cancellationToken);
    }

    internal static async Task<bool> RoomCanAcceptDepositAsync(AppDbContext context, int roomId, int bookingId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        return await context.Rooms.AsNoTracking().Where(RoomRentabilityPolicy.IsRentableForBookingAt(bookingId,
                new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc))))
            .AnyAsync(room => room.RoomId == roomId, cancellationToken);
    }
}
