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
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ICurrentUserService _currentUser;

    public InvoicesController(IInvoiceService invoiceService, ICurrentUserService currentUser)
    {
        _invoiceService = invoiceService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetAll([FromQuery] int? contractId, [FromQuery] string? status)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _invoiceService.GetAllAsync(userId, role, contractId, status));
        }
        catch (InvoiceAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (InvoiceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid invoice filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var invoice = await _invoiceService.GetByIdAsync(id, userId, role);
            if (invoice is null)
            {
                return NotFound();
            }
            return Ok(invoice);
        }
        catch (InvoiceAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpPost("preview")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceDto>> Preview(CreateInvoiceDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        return await HandleAsync(async () => Ok(await _invoiceService.PreviewAsync(dto, userId, role)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceDto>> Create(CreateInvoiceDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        return await HandleAsync(async () =>
        {
            var created = await _invoiceService.CreateAsync(dto, userId, role);
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.InvoiceId
            }, created);
        });
    }

    [HttpPost("{id:int}/payment-link")]
    [Authorize(Roles = RoleNames.Customer)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(typeof(InvoicePaymentLinkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(InvoicePaymentLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(InvoicePaymentLinkDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvoicePaymentLinkDto>> CreatePaymentLink(int id,
        [FromServices] IInvoicePaymentLinkService service, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out _)) return Unauthorized();
        try
        {
            var result = await service.CreateAsync(id, userId, cancellationToken);
            if (result.CheckoutUrl is null) return Accepted(result);
            return StatusCode(result.Reused ? 200 : 201, result);
        }
        catch (InvoiceNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Invoice not found", Detail = exception.Message }); }
        catch (InvoiceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (InvoiceBusinessRuleException exception) { return BadRequest(new ProblemDetails { Title = "Invalid invoice payment", Detail = exception.Message }); }
        catch (InvoiceStatusLinkConflictException exception) { return Conflict(new ProblemDetails { Title = "Checkout conflict", Detail = exception.Message }); }
        catch (InvoiceLinkProviderException exception) { return StatusCode(502, new ProblemDetails { Title = "Checkout creation failed", Detail = exception.Message }); }
        catch (PRN232.Application.Interfaces.CheckoutUnavailableException exception) { return StatusCode(503, new ProblemDetails { Title = "Checkout unavailable", Detail = exception.Message }); }
    }

    [HttpPost("{id:int}/pay")]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<InvoiceDto>> Pay(int id, PayInvoiceDto dto,
        [FromServices] Microsoft.Extensions.Options.IOptions<PRN232.Application.Settings.PayOSSettings> payOS)
    {
        if (payOS.Value.Enabled) return Conflict(new ProblemDetails { Title = "Use payOS checkout", Detail = "Create a payment link; simulated invoice payment is disabled." });
        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        return await HandleAsync(async () => Ok(await _invoiceService.PayAsync(id, dto, userId)));
    }

    [HttpPost("{id:int}/record-cash")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceDto>> RecordCash(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        return await HandleAsync(async () => Ok(await _invoiceService.RecordCashPaymentAsync(id, userId, role)));
    }

    // CÃ¡c thao tÃ¡c ghi Ä‘á»u nÃ©m cÃ¹ng cÃ¡c loáº¡i lá»—i nÃªn dÃ¹ng chung má»™t chá»— chuyá»ƒn exception -> mÃ£ HTTP.
    private async Task<ActionResult<InvoiceDto>> HandleAsync(Func<Task<ActionResult<InvoiceDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvoiceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid invoice", Detail = exception.Message });
        }
        catch (InvoiceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Contract not found", Detail = exception.Message });
        }
        catch (InvoiceAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (InvoiceConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invoice conflict", Detail = exception.Message });
        }
        catch (InvoicePaymentFailedException exception)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired,
                new ProblemDetails { Title = "Payment failed", Detail = exception.Message });
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
