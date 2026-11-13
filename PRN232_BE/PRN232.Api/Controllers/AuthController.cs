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
    [EnableRateLimiting("RegisterPolicy")]
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

    // POST /api/auth/google: exchange a Google ID token for our own JWT (same response as login).
    [HttpPost("google")]
    [EnableRateLimiting("LoginPolicy")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthResponseDto>> Google(GoogleLoginDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.GoogleLoginAsync(dto, cancellationToken));
        }
        catch (AuthGoogleNotConfiguredException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Google sign-in unavailable", Detail = exception.Message });
        }
        catch (AuthInvalidGoogleTokenException exception)
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid Google token", Detail = exception.Message });
        }
        catch (AuthGoogleEmailNotVerifiedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Google email not verified", Detail = exception.Message });
        }
        catch (AuthAccountInactiveException exception)
        {
            // Same response as a password login to a locked account.
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Account inactive", Detail = exception.Message });
        }
        catch (AuthGoogleAccountConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Google account conflict", Detail = exception.Message });
        }
        catch (AuthRoleNotSeededException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ProblemDetails { Title = "Server not ready", Detail = exception.Message });
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
