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
    private readonly ICurrentUserService _currentUser;

    public IdentityDocumentsController(IIdentityDocumentService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // GET /api/identity-documents?status= — Admin xem toàn bộ hồ sơ; Manager chỉ xem hồ sơ
    // của khách có booking/hợp đồng ở khu trọ của mình. Lọc theo trạng thái (vd status=Pending).
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<IdentityDocumentDto>>> GetAll([FromQuery] string? status)
    {
        try
        {
            return Ok(await _service.GetAllAsync(status, CurrentUserId(), CurrentRole()));
        }
        catch (IdentityDocumentAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    // GET /api/identity-documents/mine — khách thuê xem hồ sơ CCCD của chính mình.
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<IdentityDocumentDto>>> GetMine()
    {
        return Ok(await _service.GetMineAsync(CurrentUserId()));
    }

    // GET /api/identity-documents/{id} — Admin xem bất kỳ; Manager chỉ xem hồ sơ của khách ở
    // khu trọ của mình; khách thuê chỉ xem được hồ sơ của chính mình.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<IdentityDocumentDto>> GetById(int id)
    {
        try
        {
            var document = await _service.GetByIdAsync(id, CurrentUserId(), CurrentRole());
            if (document is null)
            {
                return NotFound();
            }
            return Ok(document);
        }
        catch (IdentityDocumentAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
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
            var updated = await _service.ReviewAsync(id, dto, CurrentUserId(), CurrentRole());
            if (updated is null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (IdentityDocumentAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (IdentityDocumentStatusTransitionException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invalid status transition", Detail = exception.Message });
        }
    }

    private int CurrentUserId()
    {
        return _currentUser.UserId
            ?? throw new InvalidOperationException("Token thiếu claim 'sub'.");
    }

    private string CurrentRole()
    {
        return _currentUser.Role ?? string.Empty;
    }

    private ObjectResult ForbidWithProblem(string detail)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = detail });
    }
}
