using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly ICurrentUserService _currentUser;

    public ContractsController(IContractService contractService, ICurrentUserService currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContractDto>>> GetAll()
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _contractService.GetAllAsync(userId, role));
        }
        catch (ContractAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContractDto>> GetById(int id)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var contract = await _contractService.GetByIdAsync(id, userId, role);
            if (contract is null)
            {
                return NotFound();
            }
            return Ok(contract);
        }
        catch (ContractAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ContractDto>> Create(CreateContractDto dto)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var created = await _contractService.CreateFromBookingAsync(dto, userId, role);
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.ContractId
            }, created);
        }
        catch (ContractBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid contract", Detail = exception.Message });
        }
        catch (ContractNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Booking not found", Detail = exception.Message });
        }
        catch (ContractAuthorizationException exception)
        {
            return ForbidWithProblem(exception.Message);
        }
        catch (ContractConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Contract cannot be created", Detail = exception.Message });
        }
    }

    private bool TryGetCurrentUser(out int userId, out string role)
    {
        userId = _currentUser.UserId ?? 0;
        role = _currentUser.Role ?? string.Empty;
        return _currentUser.IsAuthenticated && userId > 0 && role.Length > 0;
    }

    private ObjectResult ForbidWithProblem(string detail)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Forbidden", Detail = detail });
    }
}
