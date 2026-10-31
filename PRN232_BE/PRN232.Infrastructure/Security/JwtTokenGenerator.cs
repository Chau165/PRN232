using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(JwtSettings settings)
    {
        _settings = settings;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(Users user, string roleName)
    {
        var minutes = _settings.ExpiryMinutes;
        if (minutes <= 0)
        {
            minutes = 120;
        }
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("roleId", user.RoleId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
