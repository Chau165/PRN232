using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class EquipmentsController : ControllerBase
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentsController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetAll([FromQuery] int? roomId) =>
        Ok(await _equipmentService.GetAllAsync(roomId));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipmentDto>> GetById(int id)
    {
        var equipment = await _equipmentService.GetByIdAsync(id);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentDto>> Create(CreateEquipmentDto dto)
    {
        try
        {
            var created = await _equipmentService.CreateAsync(dto, CurrentUserId(), IsAdmin());
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.EquipmentId
            }, created);
        }
        catch (EquipmentForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (EquipmentBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid equipment", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EquipmentDto>> Update(int id, UpdateEquipmentDto dto)
    {
        try
        {
            var updated = await _equipmentService.UpdateAsync(id, dto, CurrentUserId(), IsAdmin());
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (EquipmentForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (EquipmentBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid equipment", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _equipmentService.DeleteAsync(id, CurrentUserId(), IsAdmin()) ? NoContent() : NotFound();
        }
        catch (EquipmentForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
    }

    private int CurrentUserId() =>
        int.Parse(User.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Token thiếu claim 'sub'."));

    private bool IsAdmin() => User.IsInRole("Admin");
}
