using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(int? roleId);
    Task<UserDto?> GetByIdAsync(int id);
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto?> UpdateRoleAsync(int id, UpdateUserRoleDto dto, int requestingUserId);
    Task<UserDto?> UpdateStatusAsync(int id, UpdateUserStatusDto dto, int requestingUserId);
    Task<UserDto?> UpdateProfileAsync(int id, UpdateProfileDto dto, int requestingUserId);
    Task ChangePasswordAsync(int id, ChangePasswordDto dto, int requestingUserId);
}
