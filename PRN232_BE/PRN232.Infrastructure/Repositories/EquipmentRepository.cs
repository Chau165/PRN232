using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
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

    public Task<List<EquipmentReadModel>> GetAllAsync(
        int? roomId,
        int? propertyId,
        string? status,
        string? search,
        int? managerId,
        int? page,
        int? pageSize)
    {
        var query = ApplyManagerScope(_context.Equipments.AsNoTracking(), managerId)
            .Where(equipment => roomId == null || equipment.RoomId == roomId)
            .Where(equipment => propertyId == null || equipment.Room!.PropertyId == propertyId)
            .Where(equipment => string.IsNullOrWhiteSpace(status) || equipment.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim().ToLower();
            query = query.Where(equipment =>
                equipment.Name.ToLower().Contains(searchTerm) ||
                equipment.Supplier.ToLower().Contains(searchTerm));
        }

        var projectedQuery = Project(query.OrderBy(equipment => equipment.EquipmentId));
        if (page.HasValue && pageSize.HasValue)
        {
            var offset = checked((page.Value - 1) * pageSize.Value);
            projectedQuery = projectedQuery.Skip(offset).Take(pageSize.Value);
        }
        return projectedQuery.ToListAsync();
    }

    public Task<EquipmentReadModel?> GetByIdAsync(int id, int? managerId)
    {
        return Project(ApplyManagerScope(_context.Equipments.AsNoTracking(), managerId)
            .Where(equipment => equipment.EquipmentId == id))
            .FirstOrDefaultAsync();
    }

    public Task<Equipments?> GetTrackedByIdAsync(int id, int? managerId)
    {
        return ApplyManagerScope(_context.Equipments, managerId)
            .FirstOrDefaultAsync(equipment => equipment.EquipmentId == id);
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

    public Task<bool> RoomExistsAsync(int roomId)
    {
        return _context.Rooms.AnyAsync(room => room.RoomId == roomId);
    }

    public Task<bool> HasRoomAccessAsync(int roomId, int managerId)
    {
        return _context.Rooms.AnyAsync(room =>
            room.RoomId == roomId &&
            room.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
    }

    public Task<bool> HasMaintenanceDependenciesAsync(int equipmentId)
    {
        return _context.MaintenanceRequests.AnyAsync(request => request.EquipmentId == equipmentId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    private static IQueryable<Equipments> ApplyManagerScope(
        IQueryable<Equipments> query,
        int? managerId)
    {
        if (!managerId.HasValue)
        {
            return query;
        }

        return query.Where(equipment =>
            equipment.Room!.Property!.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId.Value &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == RoleNames.Manager));
    }

    private static IQueryable<EquipmentReadModel> Project(IQueryable<Equipments> query)
    {
        return query.Select(equipment => new EquipmentReadModel(
            equipment.EquipmentId,
            equipment.RoomId,
            equipment.Room!.RoomCode,
            equipment.Room.PropertyId,
            equipment.Room.Property!.PropertyName,
            equipment.Name,
            equipment.InstallDate,
            equipment.WarrantyExpiry,
            equipment.Supplier,
            equipment.Status));
    }
}
