using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AuthServiceRegistrationTests
{
    [Fact]
    public async Task Public_registration_always_uses_customer_role()
    {
        var userRepository = new TestUserRepository();
        var service = new AuthService(
            userRepository,
            new TestRoleRepository(),
            new TestPasswordHasher(),
            new TestTokenGenerator());

        var response = await service.RegisterAsync(new RegisterDto(
            "New user",
            "NEW.USER@example.test",
            "password-123",
            "0900123456"));

        Assert.Equal("Customer", response.User.RoleName);
        Assert.Equal(1, response.User.RoleId);
        Assert.Equal(1, userRepository.CreatedUser?.RoleId);
        Assert.Equal("new.user@example.test", response.User.Email);
    }

    private sealed class TestUserRepository : IUserRepository
    {
        public Users? CreatedUser { get; private set; }

        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);

        public Task<Users> AddAsync(Users user)
        {
            user.UserId = 27;
            CreatedUser = user;
            return Task.FromResult(user);
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<Users?> GetByEmailAsync(string email) => throw new NotImplementedException();

        public Task<List<UserDto>> GetAllAsync(int? roleId) => throw new NotImplementedException();

        public Task<UserDto?> GetByIdAsync(int id) => throw new NotImplementedException();

        public Task<Users?> GetEntityByIdAsync(int id) => throw new NotImplementedException();

        public Task<bool> RoleExistsAsync(int roleId) => throw new NotImplementedException();
    }

    private sealed class TestRoleRepository : IRoleRepository
    {
        public Task<List<RoleDto>> GetAllAsync() => Task.FromResult(new List<RoleDto>
        {
            new(1, "Customer"),
            new(2, "Manager"),
            new(3, "Admin"),
        });

        public Task<RoleDto?> GetByIdAsync(int id) => throw new NotImplementedException();

        public Task<Roles> AddAsync(Roles role) => throw new NotImplementedException();

        public Task SaveChangesAsync() => throw new NotImplementedException();
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => throw new NotImplementedException();
    }

    private sealed class TestTokenGenerator : IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAtUtc) GenerateToken(Users user, string roleName) =>
            ("test-token", DateTime.UtcNow.AddMinutes(15));
    }
}
