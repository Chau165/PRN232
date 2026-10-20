using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Repositories;

public static class RoomAvailabilityQueries
{
    public static IQueryable<Rooms> GetRentableRooms(IQueryable<Rooms> rooms, DateTimeOffset asOfUtc) =>
        rooms
            .AsNoTracking()
            .Where(RoomRentabilityPolicy.IsRentableAt(asOfUtc));

    public static IQueryable<PropertyAvailabilityDto> GetPropertyAvailability(
        IQueryable<Property> properties,
        IQueryable<Rooms> rooms,
        DateTimeOffset asOfUtc)
    {
        var rentableRooms = GetRentableRooms(rooms, asOfUtc);

        return properties
            .AsNoTracking()
            .OrderBy(property => property.PropertyId)
            .Select(property => new PropertyAvailabilityDto(
                property.PropertyId,
                rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => room.RoomId)
                    .Distinct()
                    .Count(),
                rentableRooms
                    .Where(room => room.PropertyId == property.PropertyId)
                    .Select(room => (decimal?)room.RoomType!.Price)
                    .Min()));
    }
}
