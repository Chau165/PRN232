namespace PRN232.Application.DTOs;

public record ViewingScheduleDto(
    int ViewingId,
    int? RoomId,
    string? RoomCode,
    int PropertyId,
    string PropertyName,
    int RoomTypeId,
    string RoomTypeName,
    decimal? CurrentMonthlyRent,
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

// ScheduledAt = null nghĩa là khách để chủ trọ sắp xếp giờ xem.
public record CreateViewingScheduleDto(
    int? RoomId,
    DateTimeOffset? ScheduledAt,
    int? PropertyId = null,
    int? RoomTypeId = null);

public record SelectViewingRoomDto(int RoomId);

public record ViewingAvailableRoomDto(int RoomId, string RoomCode, decimal AreaSqm, string Status);

public record ApproveViewingScheduleDto(DateTimeOffset? ScheduledAt, int? AssignedTo, string? ManagerNote);

public record RejectViewingScheduleDto(string? Reason);
