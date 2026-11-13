using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/bookings/{bookingId:int}/refund-requests")]
[Authorize(Roles = RoleNames.Customer)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingRefundRequestsController(IBookingRefundService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BookingRefundDto>> Create(int bookingId, CreateBookingRefundDto input, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not > 0) return Unauthorized();
        try
        {
            var result = await service.CreateAsync(bookingId, currentUser.UserId.Value, input, cancellationToken);
            return CreatedAtAction(nameof(Get), new { bookingId, requestId = result.BookingRefundRequestId }, result);
        }
        catch (BookingRefundValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (BookingRefundNotFoundException) { return NotFound(); }
        catch (BookingRefundForbiddenException) { return StatusCode(403); }
        catch (BookingRefundConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpGet("{requestId:int}")]
    public async Task<ActionResult<BookingRefundDto>> Get(int bookingId, int requestId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not > 0) return Unauthorized();
        var result = await service.GetAsync(bookingId, requestId, currentUser.UserId.Value, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
