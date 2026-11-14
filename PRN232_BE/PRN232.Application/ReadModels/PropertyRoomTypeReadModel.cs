namespace PRN232.Application.ReadModels;

public sealed record PropertyRoomTypeReadModel(
    int PropertyId,
    int RoomTypeId,
    string RoomTypeName,
    decimal MonthlyRent,
    bool IsActive,
    int TotalRoomCount,
    int AvailableRoomCount);
