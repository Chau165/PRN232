using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceRequestsController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ICurrentUserService _currentUser;

    public MaintenanceRequestsController(IMaintenanceService maintenanceService, ICurrentUserService currentUser)
    {
        _maintenanceService = maintenanceService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceRequestDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? assignedTo)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try { return Ok(await _maintenanceService.GetAllAsync(userId, role, status, assignedTo)); }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (MaintenanceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid maintenance filter", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaintenanceRequestDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var request = await _maintenanceService.GetByIdAsync(id, userId, role);
            return request is null ? NotFound() : Ok(request);
        }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<MaintenanceRequestDto>> Create(CreateMaintenanceRequestDto dto)
    {
        try
        {
            if (!TryGetCurrentUser(out var userId, out _)) return Unauthorized();
            var created = await _maintenanceService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.RequestId }, created);
        }
        catch (MaintenanceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Related resource not found", Detail = exception.Message });
        }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (MaintenanceContractException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid contract", Detail = exception.Message });
        }
        catch (MaintenanceEquipmentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid equipment relationship", Detail = exception.Message });
        }
        catch (MaintenanceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Maintenance request validation failed", Detail = exception.Message });
        }
    }

    [HttpPatch("{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceRequestDto>> Update(int id, UpdateMaintenanceRequestDto dto)
    {
        try
        {
            if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
            var updated = await _maintenanceService.UpdateAsync(id, dto, userId, role);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (MaintenanceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Related resource not found", Detail = exception.Message });
        }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (MaintenanceContractException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid contract", Detail = exception.Message });
        }
        catch (MaintenanceEquipmentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid equipment relationship", Detail = exception.Message });
        }
        catch (MaintenanceAssigneeException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid assignee", Detail = exception.Message });
        }
        catch (MaintenanceStatusTransitionException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invalid status transition", Detail = exception.Message });
        }
        catch (MaintenanceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Maintenance request validation failed", Detail = exception.Message });
        }
    }

    [HttpPost("{id:int}/reports")]
    [Authorize(Roles = RoleNames.Manager)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<ActionResult<MaintenanceReportDto>> CreateReport(int id, [FromForm] CreateMaintenanceReportForm form)
    {
        try
        {
            if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
            if (form.Images is null || form.Images.Count is < 1 or > 5)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid evidence", Detail = "Submit between 1 and 5 images." });
            }

            var images = new List<MaintenanceReportImageInput>(form.Images.Count);
            foreach (var file in form.Images)
            {
                if (file.Length is 0 or > 5 * 1024 * 1024)
                {
                    return BadRequest(new ProblemDetails { Title = "Invalid evidence", Detail = "Each image must be between 1 byte and 5 MiB." });
                }
                await using var stream = file.OpenReadStream();
                using var memory = new MemoryStream((int)file.Length);
                await stream.CopyToAsync(memory);
                images.Add(new MaintenanceReportImageInput(file.FileName, file.ContentType, memory.ToArray()));
            }

            var report = await _maintenanceService.CreateReportAsync(
                id, new CreateMaintenanceReportDto(form.Description ?? string.Empty, images), userId, role);
            return CreatedAtAction(nameof(GetReport), new { id }, report);
        }
        catch (MaintenanceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Maintenance request not found", Detail = exception.Message });
        }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
        catch (MaintenanceStatusTransitionException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invalid maintenance state", Detail = exception.Message });
        }
        catch (MaintenanceBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid completion report", Detail = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Image storage unavailable", Detail = "Evidence images could not be stored." });
        }
    }

    [HttpGet("{id:int}/reports")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceReportDto>> GetReport(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var report = await _maintenanceService.GetReportAsync(id, role);
            return report is null ? NotFound() : Ok(report);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Image storage unavailable", Detail = "Evidence images could not be loaded." });
        }
    }

    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<IEnumerable<MaintenanceLogDto>>> GetLogs(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role)) return Unauthorized();
        try
        {
            var logs = await _maintenanceService.GetLogsAsync(id, userId, role);
            return logs is null ? NotFound() : Ok(logs);
        }
        catch (MaintenanceAuthorizationException exception) { return ForbidWithProblem(exception.Message); }
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

public sealed class CreateMaintenanceReportForm
{
    public string? Description { get; set; }
    public List<IFormFile> Images { get; set; } = [];
}
