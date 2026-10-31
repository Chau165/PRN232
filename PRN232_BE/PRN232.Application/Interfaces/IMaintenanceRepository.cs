using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceRepository
{
    Task<List<MaintenanceRequestReadModel>> GetAllAsync(int? customerId, int? assignedManagerId, string? status, int? assignedTo);
    Task<MaintenanceRequestReadModel?> GetByIdAsync(int id, int? customerId, int? assignedManagerId);
    Task<MaintenanceRequestStateReadModel?> GetStateByIdAsync(int id, int? assignedManagerId);
    Task<bool> RequestExistsAsync(int id);
    Task<MaintenanceContractReadModel?> GetContractAsync(int contractId);
    Task<int?> GetEquipmentRoomIdAsync(int equipmentId);
    Task<MaintenanceUserAssignmentReadModel?> GetUserAssignmentAsync(int userId);
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
    Task<List<MaintenanceLogReadModel>?> GetLogsAsync(int requestId, int? customerId, int? assignedManagerId);
    Task<int?> GetApprovingAdminIdAsync(int requestId);
    Task<MaintenanceReportReadModel?> GetReportAsync(int requestId);
    Task<bool> ReportExistsAsync(int requestId);
    Task<bool> CompleteWithReportAsync(
        int requestId,
        MaintenanceReports report,
        int changedBy,
        DateTime changedAt,
        IReadOnlyCollection<Notifications>? notifications = null);
}
