using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IRoleRepository
{
    // Trả DTO luôn — EF Core dịch Select() thành SQL chỉ lấy đúng cột cần,
    // không kéo nguyên entity về rồi mới map tay ở Service.
    Task<List<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(int id);

    // Create vẫn cần Entity vì phải có object để EF theo dõi và sinh RoleId.
    Task<Roles> AddAsync(Roles role);
    Task SaveChangesAsync();
}
