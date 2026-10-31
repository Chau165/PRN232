using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ViewingSchedulesController : ControllerBase
{
    private readonly IViewingScheduleService _viewingService;
    private readonly ICurrentUserService _currentUser;

    public ViewingSchedulesController(IViewingScheduleService viewingService, ICurrentUserService currentUser)
    {
        _viewingService = viewingService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ViewingScheduleDto>>> GetAll([FromQuery] string? status)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _viewingService.GetAllAsync(userId, role, status));
        }
        catch (ViewingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (ViewingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid viewing filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ViewingScheduleDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var viewing = await _viewingService.GetByIdAsync(id, userId, role);
            if (viewing is null)
            {
                return NotFound();
            }
            return Ok(viewing);
        }
        catch (ViewingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<ViewingScheduleDto>> Create(CreateViewingScheduleDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            var created = await _viewingService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.ViewingId
            }, created);
        }
        catch (ViewingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid viewing request", Detail = exception.Message });
        }
        catch (ViewingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Room not found", Detail = exception.Message });
        }
        catch (ViewingConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Viewing cannot be requested", Detail = exception.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    public Task<ActionResult<ViewingScheduleDto>> Approve(int id, ApproveViewingScheduleDto dto)
    {
        return HandleAsync((userId, role) => _viewingService.ApproveAsync(id, dto, userId, role));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin,Manager")]
    public Task<ActionResult<ViewingScheduleDto>> Reject(int id)
    {
        return HandleAsync((userId, role) => _viewingService.RejectAsync(id, userId, role));
    }

    // Cháº¥p thuáº­n vÃ  tá»« chá»‘i tráº£ vá» cÃ¹ng cÃ¡c loáº¡i lá»—i nÃªn dÃ¹ng chung má»™t chá»— chuyá»ƒn exception -> mÃ£ HTTP.
    private async Task<ActionResult<ViewingScheduleDto>> HandleAsync(Func<int, string, Task<ViewingScheduleDto>> action)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId, role));
        }
        catch (ViewingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid viewing decision", Detail = exception.Message });
        }
        catch (ViewingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Viewing request not found", Detail = exception.Message });
        }
        catch (ViewingAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (ViewingConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Viewing request already handled", Detail = exception.Message });
        }
    }

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
