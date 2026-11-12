using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/payment-reviews")]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PaymentReviewsController(IPaymentReviewService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<PaymentReviewDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll([FromQuery] string status = "Open", [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        HandleAsync(async () => Ok(await service.GetAllAsync(currentUser.UserId ?? 0, currentUser.Role ?? "", status, page, pageSize, cancellationToken)));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PaymentReviewDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetById(int id, CancellationToken cancellationToken) => HandleAsync(async () =>
    {
        var item = await service.GetAsync(id, currentUser.UserId ?? 0, currentUser.Role ?? "", cancellationToken);
        return item is null ? NotFound() : Ok(item);
    });

    [HttpPost("{id:int}/resolve")]
    [ProducesResponseType(typeof(PaymentReviewDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Resolve(int id, [FromBody] ResolvePaymentReviewDto dto, CancellationToken cancellationToken) =>
        HandleAsync(async () => Ok(await service.ResolveAsync(id, currentUser.UserId ?? 0, currentUser.Role ?? "", dto, cancellationToken)));

    private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (InvoiceAuthorizationException) { return Forbid(); }
        catch (InvoiceBusinessRuleException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (InvoiceNotFoundException) { return NotFound(); }
        catch (InvoiceConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
    }
}
