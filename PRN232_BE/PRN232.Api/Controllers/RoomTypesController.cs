using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomTypeDto>>> GetAll() => Ok(await _roomTypeService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomTypeDto>> GetById(int id)
    {
        var roomType = await _roomTypeService.GetByIdAsync(id);
        return roomType is null ? NotFound() : Ok(roomType);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<RoomTypeDto>> Create(CreateRoomTypeDto dto)
    {
        try
        {
            var created = await _roomTypeService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.RoomTypeId }, created);
        }
        catch (RoomTypeValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room type", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<RoomTypeDto>> Update(int id, UpdateRoomTypeDto dto)
    {
        try
        {
            var updated = await _roomTypeService.UpdateAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (RoomTypeValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room type", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _roomTypeService.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (RoomTypeHasRoomsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Room type has rooms", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}/services")]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> GetServices(int id)
    {
        var services = await _roomTypeService.GetServicesAsync(id);
        return services is null ? NotFound() : Ok(services);
    }

    [HttpPost("{id:int}/services/{serviceId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddService(int id, int serviceId)
    {
        try
        {
            await _roomTypeService.AddServiceAsync(id, serviceId);
            return NoContent();
        }
        catch (RoomTypeNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Room type not found", Detail = exception.Message });
        }
        catch (ServiceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Service not found", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}/services/{serviceId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RemoveService(int id, int serviceId) =>
        await _roomTypeService.RemoveServiceAsync(id, serviceId) ? NoContent() : NotFound();
}
