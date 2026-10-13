using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IUserRepository
{
    // Dùng cho Auth (Login) — cần PasswordHash + Role.RoleName nên trả Entity,
    // không trả DTO.
    Task<Users?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task<Users> AddAsync(Users user);

    // Dùng cho module quản lý user (Admin) — trả DTO, projection sẵn qua Select().
    Task<List<UserDto>> GetAllAsync(int? roleId);
    Task<UserDto?> GetByIdAsync(int id);

    // Update role/status/profile/password đều cần Entity để EF theo dõi thay đổi.
    Task<Users?> GetEntityByIdAsync(int id);

    Task<bool> RoleExistsAsync(int roleId);

    Task SaveChangesAsync();
}
