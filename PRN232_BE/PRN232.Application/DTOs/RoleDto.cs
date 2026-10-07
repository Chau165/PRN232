namespace PRN232.Application.DTOs;

public record RoleDto(int RoleId, string RoleName);

public record CreateRoleDto(string RoleName);
