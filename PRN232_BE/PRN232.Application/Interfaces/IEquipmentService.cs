using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IEquipmentService
{
    Task<List<EquipmentDto>> GetAllAsync(
        int? roomId,
        int? propertyId,
        string? status,
        string? search,
        int requestingUserId,
        string role,
        int? page = null,
        int? pageSize = null);
    Task<EquipmentDto?> GetByIdAsync(int id, int requestingUserId, string role);
    Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int requestingUserId, string role);
    Task<EquipmentDto?> UpdateAsync(int id, UpdateEquipmentDto dto, int requestingUserId, string role);
    Task<bool> DeleteAsync(int id, int requestingUserId, string role);
}
