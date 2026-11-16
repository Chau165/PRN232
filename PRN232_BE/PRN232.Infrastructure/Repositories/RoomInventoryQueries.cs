using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

internal static class RoomInventoryQueries
{
    // Lock order for all reservation/payment/refund/allocation writers:
    // property-room-type -> rooms in id order -> optional viewing -> booking/customer.
    // Lock every room in the type so concurrent maintenance/status updates finish before revalidation.
    internal static async Task LockAsync(AppDbContext context, int propertyId, int roomTypeId, CancellationToken ct = default)
    {
        if (!context.Database.IsNpgsql()) return;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"PropertyRoomTypes\" WHERE \"PropertyId\" = {propertyId} AND \"RoomTypeId\" = {roomTypeId} FOR UPDATE", ct);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Rooms\" WHERE \"PropertyId\" = {propertyId} AND \"RoomTypeId\" = {roomTypeId} ORDER BY \"RoomId\" FOR UPDATE", ct);
    }

    internal static async Task<bool> CanReserveAsync(AppDbContext context, int propertyId, int roomTypeId,
        int occupants, int? physicalRoomId, int? ignoredBookingId, DateTimeOffset asOfUtc, CancellationToken ct = default)
    {
        var rooms = await context.Rooms.AsNoTracking()
            .Where(room => room.PropertyId == propertyId && room.RoomTypeId == roomTypeId &&
                room.PropertyRoomType != null && room.PropertyRoomType.IsActive)
            .Where(RoomRentabilityPolicy.IsRentableForBookingAt(ignoredBookingId ?? 0, asOfUtc))
            .Select(room => new { room.RoomId, room.MaxOccupants }).ToListAsync(ct);
        var demands = await context.Bookings.AsNoTracking().Where(RoomInventoryPolicy.HoldsCapacityAt(asOfUtc))
            .Where(booking => booking.PropertyId == propertyId && booking.RoomTypeId == roomTypeId &&
                booking.RoomId == null && (ignoredBookingId == null || booking.BookingId != ignoredBookingId))
            .Select(booking => booking.OccupantCount ?? 1).ToListAsync(ct);
        if (physicalRoomId.HasValue)
        {
            var selected = rooms.SingleOrDefault(room => room.RoomId == physicalRoomId);
            if (selected is null || selected.MaxOccupants < occupants) return false;
            rooms.Remove(selected);
        }
        else demands.Add(occupants);
        return RoomInventoryPolicy.CanFit(rooms.Select(room => room.MaxOccupants), demands);
    }

    internal static async Task<List<EligibleContractRoomDto>> EligibleRoomsAsync(AppDbContext context, int bookingId,
        DateTimeOffset asOfUtc, CancellationToken ct = default)
    {
        var booking = await context.Bookings.AsNoTracking().SingleAsync(item => item.BookingId == bookingId, ct);
        if (booking.Status != BookingStatuses.Deposited ||
            await context.Contracts.AnyAsync(contract => contract.BookingId == bookingId, ct) ||
            await context.BookingRefundRequests.AnyAsync(refund => refund.BookingId == bookingId && refund.Status != BookingRefundRequestStatuses.Rejected, ct) ||
            await context.PaymentRequests.AnyAsync(request => request.BookingId == bookingId &&
                (request.Status == PaymentRequestStatuses.Creating || request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.RequiresReview), ct) ||
            !await context.Payments.AnyAsync(payment => payment.BookingId == bookingId && payment.Status == PaymentStatuses.Success &&
                payment.UserId == booking.CustomerId && payment.Amount == booking.DepositAmount, ct))
            throw new BookingStatusConflictException("Booking payment or refund requires reconciliation before room allocation.");
        var rooms = await context.Rooms.AsNoTracking()
            .Where(room => room.PropertyId == booking.PropertyId && room.RoomTypeId == booking.RoomTypeId &&
                (booking.RoomId == null || room.RoomId == booking.RoomId) && room.MaxOccupants >= (booking.OccupantCount ?? 1))
            .Where(RoomRentabilityPolicy.IsRentableForBookingAt(bookingId, asOfUtc))
            .OrderBy(room => room.RoomCode)
            .Select(room => new EligibleContractRoomDto(room.RoomId, room.RoomCode, room.PropertyId,
                room.RoomTypeId, room.MaxOccupants, room.AreaSqm)).ToListAsync(ct);
        var eligible = new List<EligibleContractRoomDto>();
        foreach (var room in rooms)
            if (await CanReserveAsync(context, booking.PropertyId, booking.RoomTypeId, booking.OccupantCount ?? 1,
                    room.RoomId, bookingId, asOfUtc, ct)) eligible.Add(room);
        return eligible;
    }
}
