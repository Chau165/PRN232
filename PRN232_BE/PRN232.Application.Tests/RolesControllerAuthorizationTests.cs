using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRN232.Api.Controllers;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class RolesControllerAuthorizationTests
{
    [Fact]
    public async Task Create_requires_admin_role()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/roles", new { roleName = "Guest" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_authenticated_non_admin_roles()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        using var response = await client.PostAsJsonAsync("/api/roles", new { roleName = "Guest" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_allows_admin_role()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.PostAsJsonAsync("/api/roles", new { roleName = "Guest" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<WebApplication> CreateTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(RolesController).Assembly);
        builder.Services.AddSingleton<IRoleService, TestRoleService>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role)],
                Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class TestRoleService : IRoleService
    {
        public Task<List<RoleDto>> GetAllAsync() => Task.FromResult(new List<RoleDto>());

        public Task<RoleDto?> GetByIdAsync(int id) => Task.FromResult<RoleDto?>(null);

        public Task<RoleDto> CreateAsync(CreateRoleDto dto) => Task.FromResult(new RoleDto(4, dto.RoleName));
    }
}
