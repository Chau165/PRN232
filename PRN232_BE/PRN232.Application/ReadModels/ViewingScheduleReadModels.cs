namespace PRN232.Application.ReadModels;

public sealed record ViewingRoomReadModel(int RoomId, string RoomCode, int OwnerId);

public sealed record ViewingManagerReadModel(int UserId, string FullName, string? PhoneNumber, string RoleName, bool IsActive);

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
    string Status,
    int? AssignedTo,
    string? AssignedManagerName,
    string? AssignedManagerPhone,
    string? ManagerNote,
    int? RespondedBy,
    DateTime? RespondedAt);
