using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceRepository
{
    Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, int? assignedManagerId, string? status, int? assignedTo);
    Task<MaintenanceRequestDto?> GetByIdAsync(int id, int? customerId, int? assignedManagerId);
    Task<MaintenanceRequestStateDto?> GetStateByIdAsync(int id, int? assignedManagerId);
    Task<bool> RequestExistsAsync(int id);
    Task<MaintenanceContractDto?> GetContractAsync(int contractId);
    Task<int?> GetEquipmentRoomIdAsync(int equipmentId);
    Task<MaintenanceUserAssignmentDto?> GetUserAssignmentAsync(int userId);
    Task<MaintenanceRequests> AddWithInitialLogAsync(
        MaintenanceRequests request,
        MaintenanceLogs log,
        Func<int, IReadOnlyCollection<Notifications>> notificationsFactory);
    Task<bool> UpdateStatusAndAddLogAsync(
        int requestId,
        string expectedStatus,
        string status,
        int? assignedTo,
        string? assignmentNote,
        int? changedBy,
        DateTime changedAt,
        IReadOnlyCollection<Notifications>? notifications = null);
    Task<List<MaintenanceLogDto>?> GetLogsAsync(int requestId, int? customerId, int? assignedManagerId);
    Task<int?> GetApprovingAdminIdAsync(int requestId);
    Task<MaintenanceReportDto?> GetReportAsync(int requestId);
    Task<bool> ReportExistsAsync(int requestId);
    Task<bool> CompleteWithReportAsync(
        int requestId,
        MaintenanceReports report,
        int changedBy,
        DateTime changedAt,
        IReadOnlyCollection<Notifications>? notifications = null);
}
