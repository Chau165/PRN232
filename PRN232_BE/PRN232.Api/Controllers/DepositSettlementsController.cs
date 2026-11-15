using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 3.6b: quyết toán cọc khi trả phòng. GET trả bản xem trước khi chưa quyết toán.
[ApiController]
[Route("api/contracts/{contractId:int}/settlement")]
[Authorize]
public sealed class DepositSettlementsController(IDepositSettlementService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<DepositSettlementDto>> Get(int contractId, CancellationToken cancellationToken) =>
        Run((userId, role) => service.GetAsync(contractId, userId, role, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Manager)]
    public Task<ActionResult<DepositSettlementDto>> Settle(int contractId, SettleDepositDto dto, CancellationToken cancellationToken) =>
        Run((userId, role) => service.SettleAsync(contractId, dto, userId, role, cancellationToken));

    private async Task<ActionResult<DepositSettlementDto>> Run(Func<int, string, Task<DepositSettlementDto>> action)
    {
        if (currentUser.UserId is not > 0 || string.IsNullOrEmpty(currentUser.Role))
        {
            return Unauthorized();
        }
        try
        {
            return Ok(await action(currentUser.UserId.Value, currentUser.Role));
        }
        catch (ContractBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid settlement", Detail = exception.Message });
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
            return Conflict(new ProblemDetails { Title = "Settlement conflict", Detail = exception.Message });
        }
    }
}
