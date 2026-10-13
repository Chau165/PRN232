namespace PRN232.Application.DTOs;

public record UserDto(
    int UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    int RoleId,
    string RoleName,
    bool IsActive);

// Admin tạo account Manager/Admin trực tiếp (role tuỳ chọn) — khác với
// /api/auth/register (công khai, luôn gán Customer).
public record CreateUserDto(
    string FullName,
    string Email,
    string Password,
    string PhoneNumber,
    int RoleId);

// Admin đổi role 1 user có sẵn (ví dụ: nâng Customer lên Manager).
public record UpdateUserRoleDto(int RoleId);

// Admin khoá/mở khoá account.
public record UpdateUserStatusDto(bool IsActive);

// Self-service: user tự sửa thông tin cá nhân — không cho đổi Email/Role/IsActive
// qua đường này.
public record UpdateProfileDto(string FullName, string PhoneNumber);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);
