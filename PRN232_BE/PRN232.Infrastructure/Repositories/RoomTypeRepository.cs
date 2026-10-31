using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class RoomTypeRepository : IRoomTypeRepository
{
    private readonly AppDbContext _context;

    public RoomTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<RoomTypes>> GetAllAsync()
    {
        return _context.RoomTypes
            .AsNoTracking()
            .OrderBy(rt => rt.RoomTypeId)
            .ToListAsync();
    }

    public Task<RoomTypes?> GetByIdAsync(int id)
    {
        return _context.RoomTypes
            .AsNoTracking()
            .Where(rt => rt.RoomTypeId == id)
            .FirstOrDefaultAsync();
    }

    public Task<RoomTypes?> GetTrackedByIdAsync(int id)
    {
        return _context.RoomTypes.FirstOrDefaultAsync(rt => rt.RoomTypeId == id);
    }

    public async Task<RoomTypes> AddAsync(RoomTypes roomType)
    {
        await _context.RoomTypes.AddAsync(roomType);
        return roomType;
    }

    public void Remove(RoomTypes roomType)
    {
        _context.RoomTypes.Remove(roomType);
    }

    public Task<bool> HasRoomsAsync(int roomTypeId)
    {
        return _context.Rooms.AnyAsync(r => r.RoomTypeId == roomTypeId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public Task<List<Service>> GetServicesAsync(int roomTypeId)
    {
        return _context.RoomTypeServices
            .AsNoTracking()
            .Where(link => link.RoomTypeId == roomTypeId)
            .Select(link => link.Service!)
            .ToListAsync();
    }

    public Task<bool> ServiceLinkExistsAsync(int roomTypeId, int serviceId)
    {
        return _context.RoomTypeServices.AnyAsync(link => link.RoomTypeId == roomTypeId && link.ServiceId == serviceId);
    }

    public async Task AddServiceLinkAsync(int roomTypeId, int serviceId)
    {
        await _context.RoomTypeServices.AddAsync(new RoomTypeServices { RoomTypeId = roomTypeId, ServiceId = serviceId });
    }

    public async Task<bool> RemoveServiceLinkAsync(int roomTypeId, int serviceId)
    {
        var link = await _context.RoomTypeServices
            .FirstOrDefaultAsync(l => l.RoomTypeId == roomTypeId && l.ServiceId == serviceId);
        if (link is null)
        {
            return false;
        }
        _context.RoomTypeServices.Remove(link);
        return true;
    }
}
