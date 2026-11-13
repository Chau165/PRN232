using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthChecksController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthChecksController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok", time = DateTime.UtcNow });
    }

    // Railway healthcheck và uptime monitor chỉ đọc mã HTTP, không đọc body: DB lỗi mà vẫn
    // trả 200 thì deploy hỏng vẫn được coi là khỏe. Trả 503 khi không kết nối được DB.
    [HttpGet("database")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDb(CancellationToken cancellationToken)
    {
        var canConnect = await _healthCheckService.IsDatabaseReachableAsync(cancellationToken);

        if (!canConnect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { database = "unreachable" });
        }
        return Ok(new { database = "connected" });
    }
}
