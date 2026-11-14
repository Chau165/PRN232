namespace PRN232.Application.DTOs;

public sealed record PropertyRoomTypeDto(
    int PropertyId,
    int RoomTypeId,
    string RoomTypeName,
    decimal MonthlyRent,
    bool IsActive,
    int TotalRoomCount,
    int AvailableRoomCount);

public sealed record CreatePropertyRoomTypeDto(int RoomTypeId, decimal MonthlyRent);

public sealed record UpdatePropertyRoomTypeDto(decimal MonthlyRent);

public sealed record UpdatePropertyRoomTypeStatusDto(bool IsActive);
