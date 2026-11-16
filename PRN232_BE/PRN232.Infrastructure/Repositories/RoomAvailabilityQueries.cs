using Microsoft.EntityFrameworkCore;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Repositories;

public static class RoomAvailabilityQueries
{
    public static IQueryable<Rooms> GetRentableRooms(IQueryable<Rooms> rooms, DateTimeOffset asOfUtc)
    {
        var physicalPolicy = RoomRentabilityPolicy.IsRentableAt(asOfUtc);
        var holdPolicy = RoomInventoryPolicy.HoldsCapacityAt(asOfUtc);
        return rooms
            .AsNoTracking()
            .Where(room => room.PropertyRoomType != null && room.PropertyRoomType.IsActive)
            .Where(physicalPolicy)
            .Where(room => room.PropertyRoomType!.Rooms.AsQueryable().Where(physicalPolicy).Count() >
                room.PropertyRoomType.Bookings.AsQueryable().Where(holdPolicy).Count(booking => booking.RoomId == null));
    }

    public static IQueryable<PropertyAvailabilityReadModel> GetPropertyAvailability(
        IQueryable<Property> properties,
        IQueryable<Rooms> rooms,
        DateTimeOffset asOfUtc)
    {
        var rentableRooms = GetRentableRooms(rooms, asOfUtc);
        var holds = RoomInventoryPolicy.HoldsCapacityAt(asOfUtc);

        return properties
            .AsNoTracking()
            .OrderBy(property => property.PropertyId)
            .Select(property => new PropertyAvailabilityReadModel(
                property.PropertyId,
                property.PropertyRoomTypes.Select(type => Math.Max(0,
                    rentableRooms.Count(room => room.PropertyId == property.PropertyId && room.RoomTypeId == type.RoomTypeId) -
                    type.Bookings.AsQueryable().Where(holds).Count(booking => booking.RoomId == null))).Sum(),
                rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => (decimal?)room.PropertyRoomType!.MonthlyRent)
                    .Min()));
    }
}
