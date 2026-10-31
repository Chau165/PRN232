using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Roles>> GetAllAsync() =>
        _context.Roles
            .AsNoTracking()
            .ToListAsync();

    public Task<Roles?> GetByIdAsync(int id) =>
        _context.Roles
            .AsNoTracking()
            .Where(r => r.RoleId == id)
            .FirstOrDefaultAsync();

    public Task<Roles> AddAsync(Roles role)
    {
        _context.Roles.Add(role);
        return Task.FromResult(role);
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
