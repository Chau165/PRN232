namespace PRN232.Application.ReadModels;

public sealed record RentableRoomReadModel(
    int RoomId,
    int PropertyId,
    int RoomTypeId,
    string RoomTypeName,
    decimal Price,
    string RoomCode,
    decimal AreaSqm);
