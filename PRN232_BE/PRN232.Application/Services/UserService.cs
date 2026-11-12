using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using DomainUser = PRN232.Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.Validation;

namespace PRN232.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository repository, IPasswordHasher passwordHasher, ILogger<UserService>? logger = null)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _logger = logger ?? NullLogger<UserService>.Instance;
    }

    public async Task<List<UserDto>> GetAllAsync(int? roleId)
    {
        var users = await _repository.GetAllAsync(roleId);
        return users.Select(ConvertToDto).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user is null)
        {
            return null;
        }
        return ConvertToDto(user);
    }

    // Admin tạo trực tiếp account với role bất kỳ (Manager/Admin/Customer) — khác
    // /api/auth/register (công khai, luôn gán Customer).
    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        var validationError = AccountValidation.FirstError(AccountValidation.CreateUser, dto);
        if (validationError is not null)
        {
            throw new UserValidationException(validationError);
        }
        if (!await _repository.RoleExistsAsync(dto.RoleId))
        {
            throw new UserRoleNotFoundException(dto.RoleId);
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(normalizedEmail))
        {
            throw new UserEmailAlreadyExistsException(normalizedEmail);
        }

        var passwordHash = _passwordHasher.Hash(dto.Password);
        var isActive = true;
        var user = ConvertToEntity(dto, normalizedEmail, passwordHash, isActive);

        var created = await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Admin created user {UserId} with role {RoleId}.", created.UserId, created.RoleId);

        return ConvertToDto(await GetUserResponseAsync(created.UserId));
    }

    public async Task<UserDto?> UpdateRoleAsync(int id, UpdateUserRoleDto dto, int requestingUserId)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (!await _repository.RoleExistsAsync(dto.RoleId))
        {
            throw new UserRoleNotFoundException(dto.RoleId);
        }
        if (entity.UserId == requestingUserId)
        {
            throw new UserCannotModifySelfException("đổi role");
        }

        var previousRoleId = entity.RoleId;
        entity.RoleId = dto.RoleId;
        await _repository.SaveChangesAsync();
        _logger.LogInformation("User {UserId} role changed from {PreviousRoleId} to {RoleId} by admin {AdminId}.",
            id, previousRoleId, dto.RoleId, requestingUserId);
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    public async Task<UserDto?> UpdateStatusAsync(int id, UpdateUserStatusDto dto, int requestingUserId)
    {
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        if (entity.UserId == requestingUserId)
        {
            throw new UserCannotModifySelfException("khoá/mở khoá");
        }

        entity.IsActive = dto.IsActive;
        await _repository.SaveChangesAsync();
        _logger.LogInformation("User {UserId} {Action} by admin {AdminId}.",
            id, dto.IsActive ? "activated" : "deactivated", requestingUserId);
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    // Self-service: chỉ chủ account mới gọi được (kiểm tra ownership ở Controller qua
    // claim "sub", Service double-check lại cho chắc).
    public async Task<UserDto?> UpdateProfileAsync(int id, UpdateProfileDto dto, int requestingUserId)
    {
        if (id != requestingUserId)
        {
            throw new UserForbiddenException();
        }

        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        var validationError = AccountValidation.FirstError(AccountValidation.UpdateProfile, dto);
        if (validationError is not null)
        {
            throw new UserValidationException(validationError);
        }

        ApplyUpdateToEntity(dto, entity);
        await _repository.SaveChangesAsync();
        var updated = await _repository.GetByIdAsync(id);
        if (updated is null)
        {
            return null;
        }
        return ConvertToDto(updated);
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordDto dto, int requestingUserId)
    {
        if (id != requestingUserId)
        {
            throw new UserForbiddenException();
        }

        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            throw new UserNotFoundException(id);
        }
        if (!_passwordHasher.Verify(dto.CurrentPassword ?? string.Empty, entity.PasswordHash))
        {
            throw new UserInvalidCurrentPasswordException();
        }
        var validationError = AccountValidation.FirstError(AccountValidation.ChangePassword, dto);
        if (validationError is not null)
        {
            throw new UserValidationException(validationError);
        }

        entity.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        await _repository.SaveChangesAsync();
    }

    private static DomainUser ConvertToEntity(
        CreateUserDto dto,
        string normalizedEmail,
        string passwordHash,
        bool isActive)
    {
        return new DomainUser
        {
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty,
            RoleId = dto.RoleId,
            IsActive = isActive
        };
    }

    private static void ApplyUpdateToEntity(UpdateProfileDto dto, DomainUser entity)
    {
        entity.FullName = dto.FullName.Trim();
        entity.PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty;
    }

    private static UserDto ConvertToDto(UserReadModel readModel)
    {
        return new UserDto(
            readModel.UserId,
            readModel.FullName,
            readModel.Email,
            readModel.PhoneNumber,
            readModel.RoleId,
            readModel.RoleName,
            readModel.IsActive);
    }

    private async Task<UserReadModel> GetUserResponseAsync(int userId)
    {
        var user = await _repository.GetByIdAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException("Created or updated user was not found.");
        }
        return user;
    }
}

public class UserBusinessRuleException(string message) : Exception(message);

public sealed class UserValidationException(string message) : UserBusinessRuleException(message);

public sealed class UserEmailAlreadyExistsException(string email)
    : UserBusinessRuleException($"Email '{email}' đã được đăng ký.");

public sealed class UserRoleNotFoundException(int roleId)
    : UserBusinessRuleException($"RoleId {roleId} không tồn tại.");

public sealed class UserNotFoundException(int id)
    : UserBusinessRuleException($"Không tìm thấy user id {id}.");

public sealed class UserForbiddenException()
    : UserBusinessRuleException("Bạn không có quyền thao tác trên account này.");

public sealed class UserCannotModifySelfException(string action)
    : UserBusinessRuleException($"Không thể tự {action} account của chính mình.");

public sealed class UserInvalidCurrentPasswordException()
    : UserBusinessRuleException("Mật khẩu hiện tại không đúng.");
