using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetAll(
        [FromQuery] bool? isRead,
        [FromQuery] string? type)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _notificationService.GetAllAsync(userId, isRead, type));
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateNotificationDto dto)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _notificationService.UpdateReadStateAsync(id, userId, dto.IsRead);
            return Ok();
        }
        catch (NotificationNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Notification not found", Detail = exception.Message });
        }
        catch (NotificationAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (NotificationBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid notification update", Detail = exception.Message });
        }
    }

    private bool TryGetCurrentUser(out int userId)
    {
        userId = _currentUser.UserId ?? 0;
        return _currentUser.IsAuthenticated && userId > 0;
    }
}
