using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UtilityReadingsController : ControllerBase
{
    private readonly IUtilityReadingService _readingService;
    private readonly ICurrentUserService _currentUser;

    public UtilityReadingsController(IUtilityReadingService readingService, ICurrentUserService currentUser)
    {
        _readingService = readingService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UtilityReadingDto>>> GetAll([FromQuery] int? contractId)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _readingService.GetAllAsync(userId, role, contractId));
        }
        catch (UtilityReadingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UtilityReadingDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var reading = await _readingService.GetByIdAsync(id, userId, role);
            return reading is null ? NotFound() : Ok(reading);
        }
        catch (UtilityReadingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<UtilityReadingDto>> Create(CreateUtilityReadingDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var created = await _readingService.CreateAsync(dto, userId, role);
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.ReadingId
            }, created);
        }
        catch (UtilityReadingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid meter reading", Detail = exception.Message });
        }
        catch (UtilityReadingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Contract not found", Detail = exception.Message });
        }
        catch (UtilityReadingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (UtilityReadingConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Reading cannot be recorded", Detail = exception.Message });
        }
    }

    private bool TryGetCurrentUser(out int userId, out string role)
    {
        userId = _currentUser.UserId ?? 0;
        role = _currentUser.Role ?? string.Empty;
        return _currentUser.IsAuthenticated && userId > 0 && role.Length > 0;
    }

    private ObjectResult ForbidWithProblem(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = detail });
}
