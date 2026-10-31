using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Application.ReadModels;

namespace PRN232.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Users?> GetByEmailAsync(string email)
    {
        return _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        return _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<Users> AddAsync(Users user)
    {
        var entry = await _context.Users.AddAsync(user);
        return entry.Entity;
    }

    public Task<List<UserReadModel>> GetAllAsync(int? roleId)
    {
        return _context.Users
            .AsNoTracking()
            .Where(u => roleId == null || u.RoleId == roleId)
            .OrderBy(u => u.UserId)
            .Select(u => new UserReadModel(
            u.UserId,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.RoleId,
            u.Role!.RoleName,
            u.IsActive))
            .ToListAsync();
    }

    public Task<UserReadModel?> GetByIdAsync(int id)
    {
        return _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == id)
            .Select(u => new UserReadModel(
            u.UserId,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.RoleId,
            u.Role!.RoleName,
            u.IsActive))
            .FirstOrDefaultAsync();
    }

    public Task<Users?> GetTrackedByIdAsync(int id)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
    }

    public Task<bool> RoleExistsAsync(int roleId)
    {
        return _context.Roles.AnyAsync(r => r.RoleId == roleId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
