using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceRequestsController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceRequestsController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceRequestDto>>> GetAll(
        [FromQuery] int? customerId,
        [FromQuery] string? status,
        [FromQuery] int? assignedTo) =>
        Ok(await _maintenanceService.GetAllAsync(customerId, status, assignedTo));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaintenanceRequestDto>> GetById(int id)
    {
        var request = await _maintenanceService.GetByIdAsync(id);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceRequestDto>> Create(CreateMaintenanceRequestDto dto)
    {
        try
        {
            var created = await _maintenanceService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.RequestId }, created);
        }
        catch (MaintenanceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Related resource not found", Detail = exception.Message });
        }
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
    public async Task<ActionResult<MaintenanceRequestDto>> Update(int id, UpdateMaintenanceRequestDto dto)
    {
        try
        {
            var updated = await _maintenanceService.UpdateAsync(id, dto, actorUserId: null);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (MaintenanceNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Related resource not found", Detail = exception.Message });
        }
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

    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<IEnumerable<MaintenanceLogDto>>> GetLogs(int id)
    {
        var logs = await _maintenanceService.GetLogsAsync(id);
        return logs is null ? NotFound() : Ok(logs);
    }
}
