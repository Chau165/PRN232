using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<List<EquipmentDto>> GetAllAsync(int? roomId);
    Task<EquipmentDto?> GetByIdAsync(int id);
    Task<Equipments?> GetEntityByIdAsync(int id);
    Task<Equipments> AddAsync(Equipments equipment);
    void Remove(Equipments equipment);
    Task<int?> GetOwnerIdAsync(int roomId);
    Task<bool> RoomExistsAsync(int roomId);
    Task SaveChangesAsync();
}
