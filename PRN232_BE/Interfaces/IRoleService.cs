using PRN232_BE.DTOs;

namespace PRN232_BE.Interfaces;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(int id);
    Task<RoleDto> CreateAsync(CreateRoleDto dto);
}
