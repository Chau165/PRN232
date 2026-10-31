namespace PRN232.Application.ReadModels;

public sealed record RoomImageReadModel(
    int RoomImageId,
    int RoomId,
    string ImageUrl,
    bool IsPrimary);
