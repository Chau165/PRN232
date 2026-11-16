using System.Linq.Expressions;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

public static class RoomInventoryPolicy
{
    // Unknown checkout outcomes retain capacity even after the ordinary Pending deadline.
    // A deposited entitlement ends only by explicit cancellation/refund or contract allocation.
    public static Expression<Func<Bookings, bool>> HoldsCapacityAt(DateTimeOffset asOfUtc)
    {
        var now = asOfUtc.UtcDateTime;
        return booking => booking.Contract == null &&
            (booking.Status == BookingStatuses.Deposited ||
             booking.Status == BookingStatuses.Pending && booking.ExpiryDate > now ||
             booking.PaymentRequests.Any(request => request.Status == PaymentRequestStatuses.Creating ||
                 request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.RequiresReview) ||
             booking.Status != BookingStatuses.Pending && booking.Status != BookingStatuses.Deposited &&
                 booking.Status != BookingStatuses.Cancelled);
    }

    // Feasibility matching, not allocation. Pair the greatest demands with the largest rooms;
    // heterogeneous room capacities must never let a small party consume the only large room.
    public static bool CanFit(IEnumerable<int> roomCapacities, IEnumerable<int> occupantCounts)
    {
        var rooms = roomCapacities.OrderDescending().ToArray();
        var demands = occupantCounts.OrderDescending().ToArray();
        return demands.Length <= rooms.Length && demands.Where((count, index) => count > rooms[index]).Any() == false;
    }
}
