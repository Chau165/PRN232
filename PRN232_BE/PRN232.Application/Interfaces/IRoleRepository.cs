using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IRoleRepository
{
    Task<List<Roles>> GetAllAsync();
    Task<Roles?> GetByIdAsync(int id);

    // Create vẫn cần Entity vì phải có object để EF theo dõi và sinh RoleId.
    Task<Roles> AddAsync(Roles role);
    Task SaveChangesAsync();
}
