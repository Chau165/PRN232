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

public record UpdateMaintenanceRequestDto(string Status, int? AssignedTo);

public record MaintenanceLogDto(
    int LogId,
    int RequestId,
    string OldStatus,
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
    int CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);
