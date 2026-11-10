using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/payment-requests")]
[Authorize(Roles = "Customer,Admin,Manager")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PaymentRequestsController(IPaymentRequestStatusService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PaymentRequestStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentRequestStatusDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not > 0) return Unauthorized();
        try
        {
            var request = await service.GetByIdAsync(id, currentUser.UserId.Value, currentUser.Role ?? string.Empty, cancellationToken);
            return request is null ? NotFound() : Ok(request);
        }
        catch (PaymentRequestStatusForbiddenException exception)
        {
            return StatusCode(403, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (PaymentRequestStatusValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid payment request ID", Detail = exception.Message });
        }
    }
}
