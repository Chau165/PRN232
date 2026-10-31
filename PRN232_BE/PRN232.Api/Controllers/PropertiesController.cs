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

    public PropertiesController(IPropertyService propertyService)
    {
        _propertyService = propertyService;
    }

    // GET /api/properties?ownerId= — route QUẢN LÝ (Admin/Manager), KHÔNG public.
    // Trả PropertyDto đầy đủ (gồm OwnerId/OwnerName) nên phải auth — browsing công khai
    // cho khách đã chuyển qua PublicPropertiesController (api/public/properties), dùng
    // DTO allow-list riêng không có thông tin chủ sở hữu (fix D9 — audit bảo mật).
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<PropertyDto>>> GetAll([FromQuery] int? ownerId)
    {
        return Ok(await _propertyService.GetAllAsync(ownerId));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PropertyDto>> GetById(int id)
    {
        var property = await _propertyService.GetByIdAsync(id);
        if (property is null)
        {
            return NotFound();
        }
        return Ok(property);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyDto dto)
    {
        try
        {
            var created = await _propertyService.CreateAsync(dto, CurrentUserId());
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.PropertyId
            }, created);
        }
        catch (PropertyValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid property", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PropertyDto>> Update(int id, UpdatePropertyDto dto)
    {
        try
        {
            var updated = await _propertyService.UpdateAsync(id, dto, CurrentUserId(), IsAdmin());
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
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
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
}
