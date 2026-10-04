using Microsoft.EntityFrameworkCore;
using PRN232_BE.Data;
using PRN232_BE.DTOs;
using PRN232_BE.Interfaces;
using PRN232_BE.Models;

namespace PRN232_BE.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    // Select() ngay trong query -> EF Core dịch thành SQL "SELECT RoleId, RoleName",
    // không SELECT * rồi mới cắt ở C#. Đọc list/detail thì nên dùng kiểu này.
    public async Task<List<RoleDto>> GetAllAsync() =>
        await _context.Roles
            .AsNoTracking()
            .Select(r => new RoleDto(r.RoleId, r.RoleName))
            .ToListAsync();

    public async Task<RoleDto?> GetByIdAsync(int id) =>
        await _context.Roles
            .AsNoTracking()
            .Where(r => r.RoleId == id)
            .Select(r => new RoleDto(r.RoleId, r.RoleName))
            .FirstOrDefaultAsync();

    // Create/Update/Delete vẫn phải làm việc với Entity (EF cần tracking để
    // sinh khoá, theo dõi thay đổi...), nên các hàm này giữ nguyên kiểu Entity.
    public async Task<Roles> AddAsync(Roles role)
    {
        _context.Roles.Add(role);
        return role;
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
