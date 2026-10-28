using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICurrentUserService _currentUser;

    public BookingsController(IBookingService bookingService, ICurrentUserService currentUser)
    {
        _bookingService = bookingService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAll([FromQuery] string? status)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try { return Ok(await _bookingService.GetAllAsync(userId, role, status)); }
        catch (BookingAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (BookingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid booking filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var booking = await _bookingService.GetByIdAsync(id, userId, role);
            return booking is null ? NotFound() : Ok(booking);
        }
        catch (BookingAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer)]
    [EnableRateLimiting("BookingPolicy")]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out _)) return Unauthorized();
        try
        {
            var created = await _bookingService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.BookingId }, created);
        }
        catch (BookingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Room not found", Detail = exception.Message });
        }
        catch (BookingRoomUnavailableException exception)
        {
            return Conflict(new ProblemDetails { Title = "Room unavailable", Detail = exception.Message });
        }
    }

    [HttpPost("{id:int}/pay")]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<BookingDto>> Pay(int id, PayBookingDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out _)) return Unauthorized();
        try
        {
            return Ok(await _bookingService.PayAsync(id, dto, userId));
        }
        catch (BookingBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid payment", Detail = exception.Message });
        }
        catch (BookingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Booking not found", Detail = exception.Message });
        }
        catch (BookingAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (BookingStatusConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Booking cannot be paid", Detail = exception.Message });
        }
        catch (BookingPaymentFailedException exception)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired,
                new ProblemDetails { Title = "Payment failed", Detail = exception.Message });
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            return Ok(await _bookingService.CancelAsync(id, userId, role));
        }
        catch (BookingNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Booking not found", Detail = exception.Message });
        }
        catch (BookingAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (BookingStatusConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Booking cannot be cancelled", Detail = exception.Message });
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
