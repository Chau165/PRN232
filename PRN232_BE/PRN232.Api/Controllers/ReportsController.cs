using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class ReportsController(IReceivablesService service, IRevenueService revenueService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevenueReportDto>> GetRevenue(
        [FromQuery] DateOnly? fromMonth, [FromQuery] DateOnly? toMonth,
        [FromQuery] int? propertyId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not > 0) return Unauthorized();
        try
        {
            return Ok(await revenueService.GetAsync(fromMonth, toMonth, propertyId, currentUser.UserId.Value,
                currentUser.Role ?? string.Empty, cancellationToken));
        }
        catch (RevenueForbiddenException exception)
        {
            return StatusCode(403, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (RevenueNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Property not found", Detail = exception.Message });
        }
        catch (RevenueValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid report filter", Detail = exception.Message });
        }
    }

    [HttpGet("receivables")]
    [ProducesResponseType(typeof(ReceivablesReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceivablesReportDto>> GetReceivables(
        [FromQuery] int? propertyId, [FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not > 0) return Unauthorized();
        try
        {
            return Ok(await service.GetAsync(propertyId, asOfDate, currentUser.UserId.Value,
                currentUser.Role ?? string.Empty, cancellationToken));
        }
        catch (ReceivablesForbiddenException exception)
        {
            return StatusCode(403, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (ReceivablesNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Property not found", Detail = exception.Message });
        }
        catch (ReceivablesValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid report filter", Detail = exception.Message });
        }
    }
}
