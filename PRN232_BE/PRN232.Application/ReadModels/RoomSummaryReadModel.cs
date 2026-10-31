namespace PRN232.Application.ReadModels;

public sealed record RoomSummaryReadModel(
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
