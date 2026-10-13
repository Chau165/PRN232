using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using Entities = PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly AppDbContext _context;

    public ServiceRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<ServiceDto>> GetAllAsync() =>
        _context.Services
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ServiceDto(s.ServiceId, s.Name, s.Price))
            .ToListAsync();

    public Task<ServiceDto?> GetByIdAsync(int id) =>
        _context.Services
            .AsNoTracking()
            .Where(s => s.ServiceId == id)
            .Select(s => new ServiceDto(s.ServiceId, s.Name, s.Price))
            .FirstOrDefaultAsync();

    public Task<Entities.Service?> GetEntityByIdAsync(int id) =>
        _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id);

    public async Task<Entities.Service> AddAsync(Entities.Service service)
    {
        await _context.Services.AddAsync(service);
        return service;
    }

    public void Remove(Entities.Service service) => _context.Services.Remove(service);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
