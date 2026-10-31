using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;

    public RoleService(IRoleRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<RoleDto>> GetAllAsync()
    {
        var roles = await _repository.GetAllAsync();
        return roles.Select(ConvertToDto).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(int id)
    {
        var role = await _repository.GetByIdAsync(id);
        return role is null ? null : ConvertToDto(role);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleDto dto)
    {
        var role = ConvertToEntity(dto);
        await _repository.AddAsync(role);
        await _repository.SaveChangesAsync();
        return ConvertToDto(role);
    }

    private static Roles ConvertToEntity(CreateRoleDto dto)
    {
        return new Roles
        {
            RoleName = dto.RoleName
        };
    }

    private static RoleDto ConvertToDto(Roles entity)
    {
        return new RoleDto(entity.RoleId, entity.RoleName);
    }
}
