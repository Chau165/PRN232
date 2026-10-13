using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IEquipmentService
{
    Task<List<EquipmentDto>> GetAllAsync(int? roomId);
    Task<EquipmentDto?> GetByIdAsync(int id);
    Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int requestingUserId, bool isAdmin);
    Task<EquipmentDto?> UpdateAsync(int id, UpdateEquipmentDto dto, int requestingUserId, bool isAdmin);
    Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin);
}
