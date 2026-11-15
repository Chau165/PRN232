using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 4.2: chuyển người đứng tên hợp đồng. Đi theo id đề nghị (không theo hợp đồng) vì người nhận chưa phải
// khách của hợp đồng cũ nên không xem được hợp đồng đó.
[ApiController]
[Route("api/contract-transfers")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContractTransfersController(IContractTransferService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<List<ContractTransferDto>>> List(CancellationToken cancellationToken) =>
        Run((userId, role) => service.ListAsync(userId, role, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    public Task<ActionResult<ContractTransferDto>> Create(CreateContractTransferDto dto, CancellationToken cancellationToken) =>
        Run((userId, role) => service.CreateAsync(dto, userId, role, cancellationToken));

    [HttpPost("{id:int}/confirm")]
    [Authorize(Roles = RoleNames.Customer)]
    public Task<ActionResult<ContractTransferDto>> Confirm(int id, CancellationToken cancellationToken) =>
        Run((userId, role) => service.ConfirmAsync(id, userId, role, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    public Task<ActionResult<ContractTransferDto>> Cancel(int id, CancelContractTransferDto dto, CancellationToken cancellationToken) =>
        Run((userId, role) => service.CancelAsync(id, dto, userId, role, cancellationToken));

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = RoleNames.Manager)]
    public Task<ActionResult<ContractTransferDto>> Complete(int id, CancellationToken cancellationToken) =>
        Run((userId, role) => service.CompleteAsync(id, userId, role, cancellationToken));

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
            return BadRequest(new ProblemDetails { Title = "Invalid contract transfer", Detail = exception.Message });
        }
        catch (PhoneNumberRequiredException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Phone number required", Detail = exception.Message });
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
            return Conflict(new ProblemDetails { Title = "Contract transfer conflict", Detail = exception.Message });
        }
    }
}
