using Microsoft.AspNetCore.Mvc;
using PRN232.Infrastructure.Data;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthChecksController : ControllerBase
{
    private readonly AppDbContext _context;

    public HealthChecksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok", time = DateTime.UtcNow });
    }

    [HttpGet("database")]
    public async Task<IActionResult> GetDb()
    {
        var canConnect = await _context.Database.CanConnectAsync();
        string databaseStatus;
        if (canConnect)
        {
            databaseStatus = "connected";
        }
        else
        {
            databaseStatus = "unreachable";
        }

        return Ok(new
        {
            database = databaseStatus
        });
    }
}
