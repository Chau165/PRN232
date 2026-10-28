using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/me/rentals")]
[Authorize(Roles = RoleNames.Customer)]
public sealed class MyRentalsController(IRentalService rentalService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RentalDto>>> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await rentalService.GetCurrentRentalsAsync(cancellationToken));
        }
        catch (RentalAuthenticationException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<RentalDto>>> GetHistory(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await rentalService.GetRentalHistoryAsync(cancellationToken));
        }
        catch (RentalAuthenticationException)
        {
            return Unauthorized();
        }
    }
}
