namespace PRN232.Application.DTOs;

public record RegisterDto(string FullName, string Email, string Password, string PhoneNumber);

public record LoginDto(string Email, string Password);

// Body of POST /api/auth/google: the ID token ("credential") from Google Identity Services.
public record GoogleLoginDto(string IdToken);

public record AuthenticatedUserDto(
    int UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    int RoleId,
    string RoleName);

public record AuthResponseDto(string AccessToken, DateTime ExpiresAtUtc, AuthenticatedUserDto User);
