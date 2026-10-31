using Microsoft.EntityFrameworkCore;
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

    public Task<List<Entities.Service>> GetAllAsync() =>
        _context.Services
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

    public Task<Entities.Service?> GetByIdAsync(int id) =>
        _context.Services
            .AsNoTracking()
            .Where(s => s.ServiceId == id)
            .FirstOrDefaultAsync();

    public Task<Entities.Service?> GetTrackedByIdAsync(int id) =>
        _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id);

    public async Task<Entities.Service> AddAsync(Entities.Service service)
    {
        await _context.Services.AddAsync(service);
        return service;
    }

    public void Remove(Entities.Service service) => _context.Services.Remove(service);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
