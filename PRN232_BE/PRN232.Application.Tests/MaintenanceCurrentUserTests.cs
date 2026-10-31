using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PRN232.Api.Security;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Security;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceCurrentUserTests
{
    [Fact]
    public void Adapter_reads_claims_issued_by_unchanged_target_jwt_generator()
    {
        var generator = new JwtTokenGenerator(new JwtSettings
        {
            Key = "test-only-signing-key-minimum-32-bytes",
            Issuer = "test",
            Audience = "test",
            ExpiryMinutes = 30
        });
        var (token, _) = generator.GenerateToken(new Users { UserId = 7, FullName = "Manager", Email = "manager@example.test", RoleId = 2 }, "Manager");
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToArray();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var current = new CurrentUserService(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        Assert.Equal("7", claims.Single(c => c.Type == "sub").Value);
        Assert.True(principal.IsInRole("Manager"));
        Assert.True(current.IsAuthenticated);
        Assert.Equal(7, current.UserId);
        Assert.Equal("Manager", current.Role);
        Assert.Equal("manager@example.test", current.Email);
        Assert.Equal("Manager", current.FullName);
    }

    [Fact]
    public void Missing_or_invalid_user_claims_do_not_supply_an_actor()
    {
        var accessor = new HttpContextAccessor();
        var current = new CurrentUserService(accessor);
        Assert.False(current.IsAuthenticated);
        Assert.Null(current.UserId);
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "invalid")], "Bearer")) };
        Assert.Null(current.UserId);
    }
}
