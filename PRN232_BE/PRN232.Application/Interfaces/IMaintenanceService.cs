using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceService
{
    Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, string? status, int? assignedTo);
    Task<MaintenanceRequestDto?> GetByIdAsync(int id);
    Task<MaintenanceRequestDto> CreateAsync(CreateMaintenanceRequestDto dto);
    Task<MaintenanceRequestDto?> UpdateAsync(int id, UpdateMaintenanceRequestDto dto, int? actorUserId);
    Task<List<MaintenanceLogDto>?> GetLogsAsync(int id);
}
