using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Services;
namespace PRN232.Api.Controllers;
[ApiController][Authorize(Roles="Admin")][Route("api/booking-identity-retention")][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class BookingIdentityRetentionController(BookingIdentityCleanupService service):ControllerBase {
 [HttpGet("preview")]public async Task<IActionResult> Preview(CancellationToken token)=>Ok(await service.RunAsync(true,token));
 [HttpPost("run")]public async Task<IActionResult> Run(CancellationToken token)=>Ok(await service.RunAsync(false,token));
}