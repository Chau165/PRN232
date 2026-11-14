using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/properties/{propertyId:int}/room-types")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class PropertyRoomTypesController : ControllerBase
{
    private readonly IPropertyRoomTypeService _service;
    private readonly ICurrentUserService _currentUser;

    public PropertyRoomTypesController(IPropertyRoomTypeService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PropertyRoomTypeDto>>> GetAll(int propertyId)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }
        try
        {
            return Ok(await _service.GetAllByPropertyIdAsync(propertyId, userId, role));
        }
        catch (PropertyRoomTypeNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Property not found", Detail = exception.Message });
        }
        catch (PropertyRoomTypeAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
    }

    [HttpGet("{roomTypeId:int}")]
    public async Task<ActionResult<PropertyRoomTypeDto>> GetById(int propertyId, int roomTypeId)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }
        try
        {
            var item = await _service.GetByIdAsync(propertyId, roomTypeId, userId, role);
            return item is null ? NotFound() : Ok(item);
        }
        catch (PropertyRoomTypeNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Property not found", Detail = exception.Message });
        }
        catch (PropertyRoomTypeAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<PropertyRoomTypeDto>> Create(int propertyId, CreatePropertyRoomTypeDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(propertyId, dto);
            return CreatedAtAction(nameof(GetById), new { propertyId, roomTypeId = created.RoomTypeId }, created);
        }
        catch (PropertyRoomTypeNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Property or room type not found", Detail = exception.Message });
        }
        catch (PropertyRoomTypeConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Room type already configured", Detail = exception.Message });
        }
        catch (PropertyRoomTypeValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid monthly rent", Detail = exception.Message });
        }
    }

    [HttpPut("{roomTypeId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<PropertyRoomTypeDto>> Update(
        int propertyId,
        int roomTypeId,
        UpdatePropertyRoomTypeDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(propertyId, roomTypeId, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (PropertyRoomTypeValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid monthly rent", Detail = exception.Message });
        }
    }

    [HttpPatch("{roomTypeId:int}/status")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<PropertyRoomTypeDto>> UpdateStatus(
        int propertyId,
        int roomTypeId,
        UpdatePropertyRoomTypeStatusDto dto)
    {
        var updated = await _service.UpdateStatusAsync(propertyId, roomTypeId, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    private bool TryGetCurrentUser(out int userId, out string role)
    {
        userId = _currentUser.UserId ?? 0;
        role = _currentUser.Role ?? string.Empty;
        return _currentUser.IsAuthenticated && userId > 0 && role.Length > 0;
    }
}
