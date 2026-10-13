using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceRepository
{
    Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, string? status, int? assignedTo);
    Task<MaintenanceRequestDto?> GetByIdAsync(int id);
    Task<MaintenanceRequestStateDto?> GetStateByIdAsync(int id);
    Task<MaintenanceContractDto?> GetContractAsync(int contractId);
    Task<int?> GetEquipmentRoomIdAsync(int equipmentId);
    Task<bool?> GetUserActiveStatusAsync(int userId);
    Task<MaintenanceRequests> AddAsync(MaintenanceRequests request);
    Task SaveChangesAsync();
    Task<bool> UpdateStatusAndAddLogAsync(
        int requestId,
        string expectedStatus,
        string status,
        int? assignedTo,
        int? changedBy,
        DateTime changedAt);
    Task<List<MaintenanceLogDto>?> GetLogsAsync(int requestId);
}
