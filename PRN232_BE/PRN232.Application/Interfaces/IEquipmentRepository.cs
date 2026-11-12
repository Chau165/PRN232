using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<List<EquipmentReadModel>> GetAllAsync(
        int? roomId,
        int? propertyId,
        string? status,
        string? search,
        int? managerId,
        int? page = null,
        int? pageSize = null);
    Task<EquipmentReadModel?> GetByIdAsync(int id, int? managerId);
    Task<Equipments?> GetTrackedByIdAsync(int id, int? managerId);
    Task<Equipments> AddAsync(Equipments equipment);
    void Remove(Equipments equipment);
    Task<bool> RoomExistsAsync(int roomId);
    Task<bool> HasRoomAccessAsync(int roomId, int managerId);
    Task<bool> HasMaintenanceDependenciesAsync(int equipmentId);
    Task SaveChangesAsync();
}
