using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/payment-requests")]
[Authorize(Roles = "Customer,Admin,Manager")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PaymentRequestsController(IPaymentRequestStatusService service, ICurrentUserService currentUser,
    IPaymentReconciliationService reconciliation) : ControllerBase
{
    [HttpPost("{id:int}/reconcile")]
    [Authorize(Roles = "Admin,Manager")]
    [EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(typeof(PaymentReconciliationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentReconciliationDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<PaymentReconciliationDto>> Reconcile(int id, CancellationToken cancellationToken, [FromQuery] bool recheckBooking = false, [FromQuery] bool recheckInvoice = false)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not > 0) return Unauthorized();
        try
        {
            var result = await reconciliation.ReconcileAsync(id, currentUser.UserId.Value, currentUser.Role ?? "", cancellationToken, recheckBooking, recheckInvoice);
            if (result is null) return NotFound();
            return result.Result is "TooRecent" or "ProviderUnavailable" or "NotFoundAtProvider"
                ? StatusCode(202, result) : Ok(result);
        }
        catch (PaymentRequestStatusForbiddenException) { return Forbid(); }
        catch (PaymentRequestStatusValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (CheckoutUnavailableException) { return StatusCode(503, new ProblemDetails { Title = "payOS is disabled." }); }
    }

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
