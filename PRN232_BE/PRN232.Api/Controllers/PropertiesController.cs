using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _propertyService;
    private readonly IPropertyManagerAssignmentService _assignmentService;

    public PropertiesController(
        IPropertyService propertyService,
        IPropertyManagerAssignmentService assignmentService)
    {
        _propertyService = propertyService;
        _assignmentService = assignmentService;
    }

    // GET /api/properties?ownerId= — route QUẢN LÝ (Admin/Manager), KHÔNG public.
    // Trả PropertyDto đầy đủ (gồm OwnerId/OwnerName) nên phải auth — browsing công khai
    // cho khách đã chuyển qua PublicPropertiesController (api/public/properties), dùng
    // DTO allow-list riêng không có thông tin chủ sở hữu (fix D9 — audit bảo mật).
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<PropertyDto>>> GetAll([FromQuery] int? ownerId)
    {
        int? managerId = IsAdmin() ? null : CurrentUserId();
        var requestedOwnerId = IsAdmin() ? ownerId : null;
        return Ok(await _propertyService.GetAllAsync(requestedOwnerId, managerId));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PropertyDto>> GetById(int id)
    {
        try
        {
            var property = await _propertyService.GetByIdAsync(
                id,
                CurrentUserId(),
                User.IsInRole("Manager"));
            if (property is null)
            {
                return NotFound();
            }
            return Ok(property);
        }
        catch (PropertyForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
    }

    [HttpGet("my-managed")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<IEnumerable<PropertyDto>>> GetMyManagedProperties(
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await _assignmentService.GetMyManagedPropertiesAsync(
                CurrentUserId(),
                "Manager",
                cancellationToken);
            return Ok(properties);
        }
        catch (PropertyManagerAssignmentAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
    }

    [HttpGet("/api/managers/{managerId:int}/properties")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<PropertyDto>>> GetPropertiesForManager(
        int managerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await _assignmentService.GetPropertiesForManagerAsync(
                managerId,
                CurrentUserId(),
                CurrentRole(),
                cancellationToken);
            return Ok(properties);
        }
        catch (PropertyManagerAssignmentAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
    }

    [HttpGet("{propertyId:int}/managers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PropertyManagerAssignmentDto>>> GetAssignmentsForProperty(
        int propertyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assignments = await _assignmentService.GetAssignmentsByPropertyAsync(
                propertyId,
                CurrentRole(),
                cancellationToken);
            return Ok(assignments);
        }
        catch (PropertyManagerAssignmentAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (PropertyManagerAssignmentNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = exception.Message });
        }
    }

    [HttpPost("{propertyId:int}/managers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PropertyManagerAssignmentDto>> AssignManager(
        int propertyId,
        AssignPropertyManagerRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _assignmentService.AssignManagerAsync(
                propertyId,
                request.ManagerId,
                CurrentUserId(),
                "Admin",
                cancellationToken);
            return CreatedAtAction(
                nameof(GetAssignmentsForProperty),
                new { propertyId },
                assignment);
        }
        catch (PropertyManagerAssignmentAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (PropertyManagerAssignmentNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = exception.Message });
        }
        catch (PropertyManagerAssignmentConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Assignment conflict", Detail = exception.Message });
        }
    }

    [HttpDelete("{propertyId:int}/managers/{managerId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeManager(
        int propertyId,
        int managerId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _assignmentService.RevokeManagerAsync(
                propertyId,
                managerId,
                CurrentUserId(),
                "Admin",
                cancellationToken);
            return NoContent();
        }
        catch (PropertyManagerAssignmentAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (PropertyManagerAssignmentNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = exception.Message });
        }
        catch (PropertyManagerAssignmentConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Assignment blocked", Detail = exception.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<PropertyDto>> Create(
        [FromForm] PropertyWriteForm form,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _propertyService.CreateAsync(
                new CreatePropertyDto(
                    form.PropertyName ?? string.Empty,
                    form.AddressText ?? string.Empty,
                    form.Ward ?? string.Empty,
                    form.District ?? string.Empty,
                    form.City ?? string.Empty,
                    form.Description),
                CurrentUserId(),
                IsAdmin(),
                await ReadImageAsync(form.PrimaryImage, cancellationToken));
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.PropertyId
            }, created);
        }
        catch (PropertyValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid property", Detail = exception.Message });
        }
        catch (PropertyGeocodingException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Geocoding unavailable", Detail = exception.Message });
        }
        catch (PropertyImageStorageException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Image storage unavailable", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<PropertyDto>> Update(
        int id,
        [FromForm] PropertyWriteForm form,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _propertyService.UpdateAsync(
                id,
                new UpdatePropertyDto(
                    form.PropertyName ?? string.Empty,
                    form.AddressText ?? string.Empty,
                    form.Ward ?? string.Empty,
                    form.District ?? string.Empty,
                    form.City ?? string.Empty,
                    form.Description),
                CurrentUserId(),
                IsAdmin(),
                await ReadImageAsync(form.PrimaryImage, cancellationToken));
            if (updated is null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (PropertyForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (PropertyValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid property", Detail = exception.Message });
        }
        catch (PropertyGeocodingException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Geocoding unavailable", Detail = exception.Message });
        }
        catch (PropertyImageStorageException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Image storage unavailable", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _propertyService.DeleteAsync(id, CurrentUserId(), IsAdmin());
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (PropertyForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (PropertyHasRoomsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Property has rooms", Detail = exception.Message });
        }
    }

    private int CurrentUserId()
    {
        var subjectId = User.FindFirst("sub")?.Value;
        if (subjectId is null)
        {
            throw new InvalidOperationException("Token thiếu claim 'sub'.");
        }
        return int.Parse(subjectId);
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    private static async Task<PropertyImageInput?> ReadImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return null;
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            throw new PropertyValidationException("Ảnh phải có dung lượng từ 1 byte đến 5 MiB.");
        }

        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await input.CopyToAsync(memory, cancellationToken);
        return new PropertyImageInput(file.FileName, file.ContentType, memory.ToArray());
    }

    private string CurrentRole()
    {
        return IsAdmin() ? "Admin" : "Manager";
    }
}

public sealed class PropertyWriteForm
{
    public string? PropertyName { get; set; }
    public string? AddressText { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Description { get; set; }
    public IFormFile? PrimaryImage { get; set; }
}
