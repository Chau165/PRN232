namespace PRN232.Application.DTOs;

public record RoomDto(
    int RoomId,
    int PropertyId,
    string PropertyName,
    int RoomTypeId,
    string RoomTypeName,
    decimal RoomTypePrice,
    string RoomCode,
    decimal AreaSqm,
    string Status,
    string? PrimaryImageUrl);

public record CreateRoomDto(int PropertyId, int RoomTypeId, string RoomCode, decimal AreaSqm);

public record UpdateRoomDto(int RoomTypeId, string RoomCode, decimal AreaSqm, string Status);

public record RoomImageDto(int RoomImageId, int RoomId, string ImageUrl, bool IsPrimary);

public record AddRoomImageDto(string ImageUrl, bool IsPrimary);
