using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IdentityDocumentsController : ControllerBase
{
    private readonly IIdentityDocumentService _service;

    public IdentityDocumentsController(IIdentityDocumentService service)
    {
        _service = service;
    }

    // GET /api/identity-documents?status= — Admin/Manager xem toàn bộ hồ sơ, lọc theo
    // trạng thái (vd status=Pending để duyệt).
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<IdentityDocumentDto>>> GetAll([FromQuery] string? status) =>
        Ok(await _service.GetAllAsync(status));

    // GET /api/identity-documents/mine — khách thuê xem hồ sơ CCCD của chính mình.
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<IdentityDocumentDto>>> GetMine() =>
        Ok(await _service.GetMineAsync(CurrentUserId()));

    // GET /api/identity-documents/{id} — Admin/Manager xem bất kỳ; khách thuê chỉ xem
    // được hồ sơ của chính mình.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<IdentityDocumentDto>> GetById(int id)
    {
        var document = await _service.GetByIdAsync(id);
        if (document is null)
        {
            return NotFound();
        }
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && document.CustomerId != CurrentUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = "Bạn chỉ có thể xem hồ sơ của chính mình." });
        }

        return Ok(document);
    }

    // POST /api/identity-documents — khách thuê nộp số CCCD (CustomerId lấy từ token).
    [HttpPost]
    public async Task<ActionResult<IdentityDocumentDto>> Submit(SubmitIdentityDocumentDto dto)
    {
        try
        {
            var created = await _service.SubmitAsync(dto, CurrentUserId());
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.IdentityDocumentId
            }, created);
        }
        catch (IdentityDocumentPendingExistsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Pending document exists", Detail = exception.Message });
        }
        catch (IdentityDocumentDuplicateException exception)
        {
            return Conflict(new ProblemDetails { Title = "CCCD already used", Detail = exception.Message });
        }
        catch (IdentityDocumentValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid document", Detail = exception.Message });
        }
    }

    // PUT /api/identity-documents/{id}/review — Admin/Manager duyệt Approved/Rejected.
    [HttpPut("{id:int}/review")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IdentityDocumentDto>> Review(int id, ReviewIdentityDocumentDto dto)
    {
        try
        {
            var updated = await _service.ReviewAsync(id, dto, CurrentUserId());
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (IdentityDocumentStatusTransitionException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invalid status transition", Detail = exception.Message });
        }
    }

    private int CurrentUserId() =>
        int.Parse(User.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Token thiếu claim 'sub'."));
}
