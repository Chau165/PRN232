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
            if (reading is null)
            {
                return NotFound();
            }
            return Ok(reading);
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
        catch (Exception exception) when (ToProblem(exception) is { } problem)
        {
            return problem;
        }
    }

    // Rebuild 4.4: kiểm tra và tính lượng dùng + cảnh báo bất thường, không lưu. Cùng quyền và cùng lỗi như khi lưu.
    [HttpPost("preview")]
    [Authorize(Roles = "Admin,Manager")]
    [PRN232.Api.Auditing.SkipAudit] // chỉ xem trước, không ghi dữ liệu.
    public async Task<ActionResult<UtilityReadingPreviewDto>> Preview(CreateUtilityReadingDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _readingService.PreviewAsync(dto, userId, role));
        }
        catch (Exception exception) when (ToProblem(exception) is { } problem)
        {
            return problem;
        }
    }

    private ObjectResult? ToProblem(Exception exception) => exception switch
    {
        UtilityReadingBusinessRuleException => BadRequest(new ProblemDetails { Title = "Invalid meter reading", Detail = exception.Message }),
        UtilityReadingNotFoundException => NotFound(new ProblemDetails { Title = "Contract not found", Detail = exception.Message }),
        UtilityReadingAuthorizationException => ForbidWithProblem(exception.Message),
        UtilityReadingConflictException => Conflict(new ProblemDetails { Title = "Reading cannot be recorded", Detail = exception.Message }),
        _ => null
    };

    private bool TryGetCurrentUser(out int userId, out string role)
    {
        userId = _currentUser.UserId ?? 0;
        role = _currentUser.Role ?? string.Empty;
        return _currentUser.IsAuthenticated && userId > 0 && role.Length > 0;
    }

    private ObjectResult ForbidWithProblem(string detail)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = detail });
    }
}
