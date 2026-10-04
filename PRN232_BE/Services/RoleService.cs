using PRN232_BE.DTOs;
using PRN232_BE.Interfaces;
using PRN232_BE.Models;

namespace PRN232_BE.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;

    public RoleService(IRoleRepository repository)
    {
        _repository = repository;
    }

    // Đọc: Repository đã trả DTO sẵn (EF Core project trực tiếp trong query),
    // Service không cần map gì thêm nữa.
    public Task<List<RoleDto>> GetAllAsync() => _repository.GetAllAsync();

    public Task<RoleDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    // Create: vẫn cần tạo Entity để EF theo dõi + sinh RoleId, nên bước này
    // vẫn "map tay" 2 chiều (CreateRoleDto -> Entity, Entity -> RoleDto) —
    // ít code, không cần thư viện mapping nào.
    public async Task<RoleDto> CreateAsync(CreateRoleDto dto)
    {
        var role = new Roles { RoleName = dto.RoleName };
        await _repository.AddAsync(role);
        await _repository.SaveChangesAsync();
        return new RoleDto(role.RoleId, role.RoleName);
    }
}
