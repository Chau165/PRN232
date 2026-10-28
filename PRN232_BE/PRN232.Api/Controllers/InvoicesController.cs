using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

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
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try { return Ok(await _invoiceService.GetAllAsync(userId, role, contractId, status)); }
        catch (InvoiceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (InvoiceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid invoice filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var invoice = await _invoiceService.GetByIdAsync(id, userId, role);
            return invoice is null ? NotFound() : Ok(invoice);
        }
        catch (InvoiceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
    }

    [HttpPost("preview")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceDto>> Preview(CreateInvoiceDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        return await HandleAsync(async () => Ok(await _invoiceService.PreviewAsync(dto, userId, role)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceDto>> Create(CreateInvoiceDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        return await HandleAsync(async () =>
        {
            var created = await _invoiceService.CreateAsync(dto, userId, role);
            return CreatedAtAction(nameof(GetById), new { id = created.InvoiceId }, created);
        });
    }

    // Xem trước và tạo thật ném cùng các loại lỗi nên dùng chung một chỗ chuyển exception -> mã HTTP.
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
        catch (InvoiceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (InvoiceConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invoice cannot be created", Detail = exception.Message });
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
