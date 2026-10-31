namespace PRN232.Application.ReadModels;

public sealed record ViewingRoomReadModel(int RoomId, string RoomCode, int OwnerId);

public sealed record ViewingScheduleReadModel(
    int ViewingId,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    DateTime? ScheduledAt,
    string Status);
