using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly AppDbContext _context;

    public EquipmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Equipments>> GetAllAsync(int? roomId)
    {
        return _context.Equipments
            .AsNoTracking()
            .Where(e => roomId == null || e.RoomId == roomId)
            .OrderBy(e => e.EquipmentId)
            .ToListAsync();
    }

    public Task<Equipments?> GetByIdAsync(int id)
    {
        return _context.Equipments
            .AsNoTracking()
            .Where(e => e.EquipmentId == id)
            .FirstOrDefaultAsync();
    }

    public Task<Equipments?> GetTrackedByIdAsync(int id)
    {
        return _context.Equipments.FirstOrDefaultAsync(e => e.EquipmentId == id);
    }

    public async Task<Equipments> AddAsync(Equipments equipment)
    {
        await _context.Equipments.AddAsync(equipment);
        return equipment;
    }

    public void Remove(Equipments equipment)
    {
        _context.Equipments.Remove(equipment);
    }

    public Task<int?> GetOwnerIdAsync(int roomId)
    {
        return _context.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .Select(r => (int?)r.Property!.OwnerId)
            .FirstOrDefaultAsync();
    }

    public Task<bool> RoomExistsAsync(int roomId)
    {
        return _context.Rooms.AnyAsync(r => r.RoomId == roomId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
