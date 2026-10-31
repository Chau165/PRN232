using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetAll(
        [FromQuery] bool? isRead,
        [FromQuery] string? type)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await notificationService.GetAllAsync(userId, isRead, type));
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
            await notificationService.UpdateReadStateAsync(id, userId, dto.IsRead);
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
        userId = currentUser.UserId ?? 0;
        return currentUser.IsAuthenticated && userId > 0;
    }
}
