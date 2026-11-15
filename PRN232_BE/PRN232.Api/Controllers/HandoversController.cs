using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Api.Validation;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 3.5: biên bản bàn giao thiết bị lúc nhận phòng và trả phòng của một hợp đồng.
[ApiController]
[Route("api/contracts/{contractId:int}/handovers")]
[Authorize]
public sealed class HandoversController(IHandoverService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<ContractHandoversDto>> Get(int contractId, CancellationToken cancellationToken) =>
        Run(userId => service.GetAsync(contractId, userId, currentUser.Role!, cancellationToken));

    [HttpPut("move-in")]
    [Authorize(Roles = RoleNames.Manager)]
    public Task<ActionResult<ContractHandoversDto>> SaveMoveIn(int contractId, SaveHandoverDto dto, CancellationToken cancellationToken) =>
        Run(userId => service.SaveAsync(contractId, HandoverKinds.MoveIn, dto, userId, currentUser.Role!, cancellationToken));

    [HttpPut("move-out")]
    [Authorize(Roles = RoleNames.Manager)]
    public Task<ActionResult<ContractHandoversDto>> SaveMoveOut(int contractId, SaveHandoverDto dto, CancellationToken cancellationToken) =>
        Run(userId => service.SaveAsync(contractId, HandoverKinds.MoveOut, dto, userId, currentUser.Role!, cancellationToken));

    private async Task<ActionResult<ContractHandoversDto>> Run(Func<int, Task<ContractHandoversDto>> action)
    {
        if (currentUser.UserId is not > 0 || string.IsNullOrEmpty(currentUser.Role))
        {
            return Unauthorized();
        }
        try
        {
            return Ok(await action(currentUser.UserId.Value));
        }
        catch (HandoverItemException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.FieldKey] = [exception.Message]
            })
            {
                Title = FluentValidationFilter.Title,
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (ContractBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid handover", Detail = exception.Message });
        }
        catch (ContractNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Contract not found", Detail = exception.Message });
        }
        catch (ContractAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (ContractConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Handover conflict", Detail = exception.Message });
        }
    }
}
