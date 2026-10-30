using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceService
{
    Task<List<MaintenanceRequestDto>> GetAllAsync(int userId, string role, string? status, int? assignedTo);
    Task<MaintenanceRequestDto?> GetByIdAsync(int id, int userId, string role);
    Task<MaintenanceRequestDto> CreateAsync(CreateMaintenanceRequestDto dto, int customerId);
    Task<MaintenanceRequestDto?> UpdateAsync(int id, UpdateMaintenanceRequestDto dto, int actorUserId, string role);
    Task<List<MaintenanceLogDto>?> GetLogsAsync(int id, int userId, string role);
    Task<MaintenanceReportDto> CreateReportAsync(int id, CreateMaintenanceReportDto dto, int managerId, string role);
    Task<MaintenanceReportDto?> GetReportAsync(int id, int userId, string role);
}
