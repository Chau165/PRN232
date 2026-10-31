using PRN232.Domain.Entities;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IUserRepository
{
    // Dùng cho Auth (Login) — cần PasswordHash + Role.RoleName nên trả Entity,
    // không trả DTO.
    Task<Users?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task<Users> AddAsync(Users user);

    Task<List<UserReadModel>> GetAllAsync(int? roleId);
    Task<UserReadModel?> GetByIdAsync(int id);

    // Update role/status/profile/password đều cần Entity để EF theo dõi thay đổi.
    Task<Users?> GetTrackedByIdAsync(int id);

    Task<bool> RoleExistsAsync(int roleId);

    Task SaveChangesAsync();
}
