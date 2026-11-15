using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 4.1: người ở cùng của một hợp đồng. Danh sách chỉ có CCCD đã che; xem đủ số là POST riêng
// (được AuditLogFilter ghi nhật ký), phản hồi không cho trình duyệt lưu cache.
[ApiController]
[Route("api/contracts/{contractId:int}/occupants")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContractOccupantsController(IContractOccupantService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<ContractOccupantsDto>> Get(int contractId, CancellationToken cancellationToken) =>
        Run((userId, role) => service.GetAsync(contractId, userId, role, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    public Task<ActionResult<ContractOccupantsDto>> Add(int contractId, AddContractOccupantDto dto, CancellationToken cancellationToken) =>
        Run((userId, role) => service.AddAsync(contractId, dto, userId, role, cancellationToken));

    [HttpPut("{occupantId:int}/move-out")]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    public Task<ActionResult<ContractOccupantsDto>> RecordMoveOut(int contractId, int occupantId, RecordOccupantMoveOutDto dto,
        CancellationToken cancellationToken) =>
        Run((userId, role) => service.RecordMoveOutAsync(contractId, occupantId, dto, userId, role, cancellationToken));

    [HttpPost("{occupantId:int}/reveal-identity")]
    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.Admin)]
    public Task<ActionResult<RevealedIdentityDto>> RevealIdentity(int contractId, int occupantId, CancellationToken cancellationToken) =>
        Run((userId, role) => service.RevealIdentityAsync(contractId, occupantId, userId, role, cancellationToken));

    private async Task<ActionResult<T>> Run<T>(Func<int, string, Task<T>> action)
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
            return BadRequest(new ProblemDetails { Title = "Invalid occupant", Detail = exception.Message });
        }
        catch (ContractNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = exception.Message });
        }
        catch (ContractAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = exception.Message });
        }
        catch (ContractConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Occupant conflict", Detail = exception.Message });
        }
    }
}
