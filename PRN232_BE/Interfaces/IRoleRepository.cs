using PRN232_BE.DTOs;
using PRN232_BE.Models;

namespace PRN232_BE.Interfaces;

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
