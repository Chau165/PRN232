using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<List<Equipments>> GetAllAsync(int? roomId);
    Task<Equipments?> GetByIdAsync(int id);
    Task<Equipments?> GetTrackedByIdAsync(int id);
    Task<Equipments> AddAsync(Equipments equipment);
    void Remove(Equipments equipment);
    Task<int?> GetOwnerIdAsync(int roomId);
    Task<bool> RoomExistsAsync(int roomId);
    Task SaveChangesAsync();
}
