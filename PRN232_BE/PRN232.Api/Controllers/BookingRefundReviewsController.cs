using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/booking-refund-requests")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingRefundReviewsController(IBookingRefundReviewService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken, [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try { return Ok(await service.ListAsync(status, page, pageSize, cancellationToken)); }
        catch (BookingRefundValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpGet("{requestId:int}")]
    public async Task<IActionResult> GetById(int requestId, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(requestId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{requestId:int}/review")]
    public async Task<IActionResult> Review(int requestId, ReviewBookingRefundDto input, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not > 0) return Unauthorized();
        try { return Ok(await service.ReviewAsync(requestId, currentUser.UserId.Value, input, cancellationToken)); }
        catch (BookingRefundValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (BookingRefundNotFoundException) { return NotFound(); }
        catch (BookingRefundForbiddenException) { return StatusCode(403); }
        catch (BookingRefundConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
    }
}
