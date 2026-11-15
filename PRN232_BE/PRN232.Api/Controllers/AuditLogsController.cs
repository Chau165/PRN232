using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 5.1: trang Admin xem nhật ký thao tác.
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuditLogsController(IAuditLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> Search([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.SearchAsync(query, cancellationToken));
        }
        catch (AuditLogQueryException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid audit log filter", Detail = exception.Message });
        }
    }
}
