namespace PRN232.Application.DTOs;

public record MaintenanceRequestDto(
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
    string? AssignedStaffName);

public record CreateMaintenanceRequestDto(int ContractId, int? EquipmentId, string Description);

public record UpdateMaintenanceRequestDto(int? AssignedTo);

public record MaintenanceLogDto(
    int LogId,
    int RequestId,
    string? OldStatus,
    string NewStatus,
    int? ChangedBy,
    string? ChangedByName,
    DateTime ChangedAt);

public record MaintenanceRequestStateDto(
    int RequestId,
    int ContractId,
    int? EquipmentId,
    int? AssignedTo,
    string Status);

public record MaintenanceContractDto(
    int ContractId,
    int RoomId,
    string? RoomCode,
    int CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public record MaintenanceUserAssignmentDto(bool IsActive, string RoleName);

public record MaintenanceReportDto(
    int ReportId,
    int RequestId,
    string Description,
    int ReportedBy,
    string? ReportedByName,
    DateTime CreatedAt,
    IReadOnlyList<MaintenanceReportImageDto> Images);

public record MaintenanceReportImageDto(int ImageId, string ImageUrl);

public record CreateMaintenanceReportDto(string Description, IReadOnlyList<MaintenanceReportImageInput> Images);

public record MaintenanceReportImageInput(string FileName, string ContentType, byte[] Content);
