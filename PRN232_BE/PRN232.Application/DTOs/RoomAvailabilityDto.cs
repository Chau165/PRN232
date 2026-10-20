namespace PRN232.Application.DTOs;

public record RentableRoomDto(
    int RoomId,
    int PropertyId,
    int RoomTypeId,
    string RoomTypeName,
    decimal Price,
    string RoomCode,
    decimal AreaSqm);

public record PropertyAvailabilityDto(
    int PropertyId,
    int AvailableRoomCount,
    decimal? StartingPrice);
