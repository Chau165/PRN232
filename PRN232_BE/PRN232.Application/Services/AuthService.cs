using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class AuthService : IAuthService
{
    public const string DefaultRegisterRoleName = "Customer";

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            throw new AuthValidationException("Họ tên không được để trống.");
        }
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
        {
            throw new AuthValidationException("Email không hợp lệ.");
        }
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
        {
            throw new AuthValidationException("Mật khẩu phải có ít nhất 6 ký tự.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await _userRepository.EmailExistsAsync(normalizedEmail))
        {
            throw new AuthEmailAlreadyExistsException(normalizedEmail);
        }

        // Đăng ký công khai luôn gán role "Customer" — tạo tài khoản Admin/Manager
        // là việc riêng của module quản lý user (admin-only), không đi qua đây.
        var roles = await _roleRepository.GetAllAsync();
        var customerRole = roles.FirstOrDefault(r =>
            string.Equals(r.RoleName, DefaultRegisterRoleName, StringComparison.OrdinalIgnoreCase));
        if (customerRole is null)
        {
            throw new AuthRoleNotSeededException(DefaultRegisterRoleName);
        }

        var passwordHash = _passwordHasher.Hash(dto.Password);
        var isActive = true;
        var user = ConvertToEntity(dto, normalizedEmail, passwordHash, customerRole.RoleId, isActive);

        var created = await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return BuildAuthResponse(created, customerRole.RoleName);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null || !_passwordHasher.Verify(dto.Password ?? string.Empty, user.PasswordHash))
        {
            throw new AuthInvalidCredentialsException();
        }
        if (!user.IsActive)
        {
            throw new AuthAccountInactiveException();
        }

        return BuildAuthResponse(user, user.Role?.RoleName ?? string.Empty);
    }

    private static Users ConvertToEntity(
        RegisterDto dto,
        string normalizedEmail,
        string passwordHash,
        int customerRoleId,
        bool isActive)
    {
        return new Users
        {
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty,
            RoleId = customerRoleId,
            IsActive = isActive
        };
    }

    private AuthResponseDto BuildAuthResponse(Users user, string roleName)
    {
        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user, roleName);
        var userDto = ConvertToDto(user, roleName);
        return new AuthResponseDto(token, expiresAtUtc, userDto);
    }

    private static AuthenticatedUserDto ConvertToDto(Users user, string roleName)
    {
        return new AuthenticatedUserDto(
            user.UserId,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.RoleId,
            roleName);
    }
}

public class AuthBusinessRuleException(string message) : Exception(message);

public sealed class AuthValidationException(string message) : AuthBusinessRuleException(message);

public sealed class AuthEmailAlreadyExistsException(string email)
    : AuthBusinessRuleException($"Email '{email}' đã được đăng ký.");

public sealed class AuthInvalidCredentialsException()
    : AuthBusinessRuleException("Email hoặc mật khẩu không đúng.");

public sealed class AuthAccountInactiveException()
    : AuthBusinessRuleException("Tài khoản đã bị khoá.");

public sealed class AuthRoleNotSeededException(string roleName)
    : AuthBusinessRuleException($"Role '{roleName}' chưa tồn tại trong DB — cần seed Roles trước.");
