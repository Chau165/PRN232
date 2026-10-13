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

    // GET /api/properties — browse công khai, không cần login.
    // GET /api/properties?ownerId=5 — Manager xem khu trọ của riêng mình.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PropertyDto>>> GetAll([FromQuery] int? ownerId) =>
        Ok(await _propertyService.GetAllAsync(ownerId));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PropertyDto>> GetById(int id)
    {
        var property = await _propertyService.GetByIdAsync(id);
        return property is null ? NotFound() : Ok(property);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyDto dto)
    {
        try
        {
            var created = await _propertyService.CreateAsync(dto, CurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.PropertyId }, created);
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
            return updated is null ? NotFound() : Ok(updated);
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
            return deleted ? NoContent() : NotFound();
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

    private int CurrentUserId() =>
        int.Parse(User.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Token thiếu claim 'sub'."));

    private bool IsAdmin() => User.IsInRole("Admin");
}
