using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.Validation;

namespace PRN232.Application.Services;

public class AuthService : IAuthService
{
    public const string DefaultRegisterRoleName = "Customer";

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;
    private readonly IGoogleIdTokenValidator? _googleIdTokenValidator;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService>? logger = null,
        IGoogleIdTokenValidator? googleIdTokenValidator = null)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger ?? NullLogger<AuthService>.Instance;
        _googleIdTokenValidator = googleIdTokenValidator;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var validationError = AccountValidation.FirstError(AccountValidation.Register, dto);
        if (validationError is not null)
        {
            throw new AuthValidationException(validationError);
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await _userRepository.EmailExistsAsync(normalizedEmail))
        {
            throw new AuthEmailAlreadyExistsException(normalizedEmail);
        }

        // Đăng ký công khai luôn gán role "Customer" — tạo tài khoản Admin/Manager
        // là việc riêng của module quản lý user (admin-only), không đi qua đây.
        var customerRole = await GetCustomerRoleAsync();

        var passwordHash = _passwordHasher.Hash(dto.Password);
        var isActive = true;
        var user = ConvertToEntity(dto, normalizedEmail, passwordHash, customerRole.RoleId, isActive);

        var created = await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();
        _logger.LogInformation("User {UserId} registered as {Role}.", created.UserId, customerRole.RoleName);

        return BuildAuthResponse(created, customerRole.RoleName);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        // Không ghi email hay mật khẩu vào log: chỉ ghi UserId khi đã biết tài khoản.
        if (user is null)
        {
            _logger.LogWarning("Login failed: unknown account.");
            throw new AuthInvalidCredentialsException();
        }
        // Accounts created through Google have no password yet: same failure as a wrong
        // password, so the response does not reveal how the account signs in.
        if (user.PasswordHash is null)
        {
            _logger.LogWarning("Login failed for user {UserId}: account has no password.", user.UserId);
            throw new AuthInvalidCredentialsException();
        }
        if (!_passwordHasher.Verify(dto.Password ?? string.Empty, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for user {UserId}: wrong password.", user.UserId);
            throw new AuthInvalidCredentialsException();
        }
        if (!user.IsActive)
        {
            _logger.LogWarning("Login refused for user {UserId}: account is inactive.", user.UserId);
            throw new AuthAccountInactiveException();
        }

        _logger.LogInformation("User {UserId} logged in.", user.UserId);
        return BuildAuthResponse(user, user.Role?.RoleName ?? string.Empty);
    }

    // "Sign in with Google": verify the Google ID token, find or create the user, then issue our
    // own JWT exactly like password login, so roles and the active-account check stay the same.
    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto dto, CancellationToken cancellationToken)
    {
        if (_googleIdTokenValidator is not { IsConfigured: true })
        {
            throw new AuthGoogleNotConfiguredException();
        }
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            _logger.LogWarning("Google login failed: empty ID token.");
            throw new AuthInvalidGoogleTokenException();
        }

        var identity = await _googleIdTokenValidator.ValidateAsync(dto.IdToken, cancellationToken);
        if (identity is null)
        {
            _logger.LogWarning("Google login failed: invalid ID token.");
            throw new AuthInvalidGoogleTokenException();
        }
        if (!identity.EmailVerified)
        {
            _logger.LogWarning("Google login refused: Google email is not verified.");
            throw new AuthGoogleEmailNotVerifiedException();
        }

        var user = await _userRepository.GetByGoogleSubjectAsync(identity.Subject);
        var roleName = user?.Role?.RoleName ?? string.Empty;
        if (user is null)
        {
            var normalizedEmail = identity.Email.Trim().ToLowerInvariant();
            user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user is not null)
            {
                if (user.GoogleSubject is not null)
                {
                    _logger.LogWarning(
                        "Google login refused for user {UserId}: already linked to another Google account.",
                        user.UserId);
                    throw new AuthGoogleAccountConflictException();
                }
                EnsureActive(user, "Google login");
                roleName = user.Role?.RoleName ?? string.Empty;
                user.GoogleSubject = identity.Subject;
                await _userRepository.SaveChangesAsync();
                _logger.LogInformation("Google account linked to user {UserId}.", user.UserId);
            }
            else
            {
                var customerRole = await GetCustomerRoleAsync();
                user = await _userRepository.AddAsync(new Users
                {
                    FullName = GoogleDisplayName(identity, normalizedEmail),
                    Email = normalizedEmail,
                    PasswordHash = null,
                    GoogleSubject = identity.Subject,
                    PhoneNumber = string.Empty,
                    RoleId = customerRole.RoleId,
                    IsActive = true
                });
                roleName = customerRole.RoleName;
                await _userRepository.SaveChangesAsync();
                _logger.LogInformation("User {UserId} registered through Google as {Role}.", user.UserId, customerRole.RoleName);
            }
        }

        EnsureActive(user, "Google login");
        _logger.LogInformation("User {UserId} logged in with Google.", user.UserId);
        return BuildAuthResponse(user, roleName);
    }

    private void EnsureActive(Users user, string action)
    {
        if (!user.IsActive)
        {
            _logger.LogWarning("{Action} refused for user {UserId}: account is inactive.", action, user.UserId);
            throw new AuthAccountInactiveException();
        }
    }

    private static string GoogleDisplayName(GoogleIdentity identity, string normalizedEmail)
    {
        if (!string.IsNullOrWhiteSpace(identity.Name))
        {
            return identity.Name.Trim();
        }
        var at = normalizedEmail.IndexOf('@');
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    private async Task<Roles> GetCustomerRoleAsync()
    {
        var roles = await _roleRepository.GetAllAsync();
        return roles.FirstOrDefault(r =>
                string.Equals(r.RoleName, DefaultRegisterRoleName, StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthRoleNotSeededException(DefaultRegisterRoleName);
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

public sealed class AuthGoogleNotConfiguredException()
    : AuthBusinessRuleException("Đăng nhập bằng Google chưa được cấu hình trên máy chủ.");

public sealed class AuthInvalidGoogleTokenException()
    : AuthBusinessRuleException("Phiên đăng nhập Google không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.");

public sealed class AuthGoogleEmailNotVerifiedException()
    : AuthBusinessRuleException("Email của tài khoản Google chưa được xác minh.");

public sealed class AuthGoogleAccountConflictException()
    : AuthBusinessRuleException("Email này đã được liên kết với một tài khoản Google khác.");

public sealed class AuthRoleNotSeededException(string roleName)
    : AuthBusinessRuleException($"Role '{roleName}' chưa tồn tại trong DB — cần seed Roles trước.");
