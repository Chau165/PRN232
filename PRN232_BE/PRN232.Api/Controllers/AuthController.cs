using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        try
        {
            return Ok(await _authService.RegisterAsync(dto));
        }
        catch (AuthEmailAlreadyExistsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Email already registered", Detail = exception.Message });
        }
        catch (AuthRoleNotSeededException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ProblemDetails { Title = "Server not ready", Detail = exception.Message });
        }
        catch (AuthBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Registration failed", Detail = exception.Message });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        try
        {
            return Ok(await _authService.LoginAsync(dto));
        }
        catch (AuthInvalidCredentialsException exception)
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials", Detail = exception.Message });
        }
        catch (AuthAccountInactiveException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Account inactive", Detail = exception.Message });
        }
        catch (AuthBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Login failed", Detail = exception.Message });
        }
    }
}
