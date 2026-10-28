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
public class InvoiceComplaintsController : ControllerBase
{
    private readonly IInvoiceComplaintService _complaintService;
    private readonly ICurrentUserService _currentUser;

    public InvoiceComplaintsController(IInvoiceComplaintService complaintService, ICurrentUserService currentUser)
    {
        _complaintService = complaintService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceComplaintDto>>> GetAll([FromQuery] string? status)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try { return Ok(await _complaintService.GetAllAsync(userId, role, status)); }
        catch (ComplaintAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (ComplaintBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid complaint filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceComplaintDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var complaint = await _complaintService.GetByIdAsync(id, userId, role);
            return complaint is null ? NotFound() : Ok(complaint);
        }
        catch (ComplaintAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<InvoiceComplaintDto>> Create(CreateInvoiceComplaintDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out _)) return Unauthorized();
        return await HandleAsync(async () =>
        {
            var created = await _complaintService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.ComplaintId }, created);
        });
    }

    [HttpPatch("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<InvoiceComplaintDto>> UpdateStatus(int id, UpdateComplaintStatusDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        return await HandleAsync(async () => Ok(await _complaintService.UpdateStatusAsync(id, dto, userId, role)));
    }

    // Gửi và cập nhật khiếu nại ném cùng các loại lỗi nên dùng chung một chỗ chuyển exception -> mã HTTP.
    private async Task<ActionResult<InvoiceComplaintDto>> HandleAsync(Func<Task<ActionResult<InvoiceComplaintDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (ComplaintBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid complaint", Detail = exception.Message });
        }
        catch (ComplaintNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = exception.Message });
        }
        catch (ComplaintAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
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
