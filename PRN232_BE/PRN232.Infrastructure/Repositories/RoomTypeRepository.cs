using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
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

    public Task<List<RoomTypeDto>> GetAllAsync() =>
        _context.RoomTypes
            .AsNoTracking()
            .OrderBy(rt => rt.RoomTypeId)
            .Select(rt => new RoomTypeDto(rt.RoomTypeId, rt.TypeName, rt.Price))
            .ToListAsync();

    public Task<RoomTypeDto?> GetByIdAsync(int id) =>
        _context.RoomTypes
            .AsNoTracking()
            .Where(rt => rt.RoomTypeId == id)
            .Select(rt => new RoomTypeDto(rt.RoomTypeId, rt.TypeName, rt.Price))
            .FirstOrDefaultAsync();

    public Task<RoomTypes?> GetEntityByIdAsync(int id) =>
        _context.RoomTypes.FirstOrDefaultAsync(rt => rt.RoomTypeId == id);

    public async Task<RoomTypes> AddAsync(RoomTypes roomType)
    {
        await _context.RoomTypes.AddAsync(roomType);
        return roomType;
    }

    public void Remove(RoomTypes roomType) => _context.RoomTypes.Remove(roomType);

    public Task<bool> HasRoomsAsync(int roomTypeId) =>
        _context.Rooms.AnyAsync(r => r.RoomTypeId == roomTypeId);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public Task<List<ServiceDto>> GetServicesAsync(int roomTypeId) =>
        _context.RoomTypeServices
            .AsNoTracking()
            .Where(link => link.RoomTypeId == roomTypeId)
            .Select(link => new ServiceDto(link.Service!.ServiceId, link.Service.Name, link.Service.Price))
            .ToListAsync();

    public Task<bool> ServiceLinkExistsAsync(int roomTypeId, int serviceId) =>
        _context.RoomTypeServices.AnyAsync(link => link.RoomTypeId == roomTypeId && link.ServiceId == serviceId);

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
