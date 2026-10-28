using System.Linq.Expressions;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

public static class RoomRentabilityPolicy
{
    private static readonly string[] RecognizedBookingStatuses =
        { BookingStatuses.Pending, BookingStatuses.Deposited, BookingStatuses.Cancelled };
    private static readonly string[] RecognizedContractStatuses = { ContractStatuses.Active };

    public static Expression<Func<Rooms, bool>> IsRentableAt(DateTimeOffset asOfUtc)
    {
        var instantUtc = asOfUtc.UtcDateTime;

        return room =>
            room.Status == RoomStatuses.Available &&
            !room.Bookings.Any(booking =>
                booking.Status == BookingStatuses.Deposited ||
                (booking.Status == BookingStatuses.Pending && booking.ExpiryDate > instantUtc) ||
                !RecognizedBookingStatuses.Contains(booking.Status)) &&
            !room.Contracts.Any(contract =>
                contract.Status == ContractStatuses.Active ||
                !RecognizedContractStatuses.Contains(contract.Status));
    }
}
