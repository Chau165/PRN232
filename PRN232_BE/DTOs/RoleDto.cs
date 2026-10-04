namespace PRN232_BE.DTOs;

public record RoleDto(int RoleId, string RoleName);

public record CreateRoleDto(string RoleName);
