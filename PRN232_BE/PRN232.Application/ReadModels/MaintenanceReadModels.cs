namespace PRN232.Application.ReadModels;

public sealed record MaintenanceRequestReadModel(
    int RequestId,
    string Description,
    string Status,
    int ContractId,
    int CustomerId,
    string? CustomerName,
    int RoomId,
    string? RoomCode,
    int? EquipmentId,
    string? EquipmentName,
    string? EquipmentStatus,
    int? AssignedTo,
    string? AssignedStaffName,
    string? AssignmentNote = null);

public sealed record MaintenanceRequestStateReadModel(
    int RequestId,
    int ContractId,
    int? EquipmentId,
    int? AssignedTo,
    string Status);

public sealed record MaintenanceContractReadModel(
    int ContractId,
    int RoomId,
    string? RoomCode,
    int CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public sealed record MaintenanceUserAssignmentReadModel(bool IsActive, string RoleName);

public sealed record MaintenanceLogReadModel(
    int LogId,
    int RequestId,
    string? OldStatus,
    string NewStatus,
    int? ChangedBy,
    string? ChangedByName,
    DateTime ChangedAt);

public sealed record MaintenanceReportReadModel(
    int ReportId,
    int RequestId,
    string Description,
    int ReportedBy,
    string? ReportedByName,
    DateTime CreatedAt,
    IReadOnlyList<MaintenanceReportImageReadModel> Images);

public sealed record MaintenanceReportImageReadModel(int ImageId, string ImageUrl);
