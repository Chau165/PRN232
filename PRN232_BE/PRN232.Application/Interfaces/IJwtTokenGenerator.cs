using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(Users user, string roleName);
}
