using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/payments/payos/webhook")]
[AllowAnonymous] // payOS authenticates through its signature, not an application JWT.
public class PayOSWebhookController(IPayOSWebhookService service) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            await service.ReceiveAsync(payload.GetRawText(), cancellationToken);
            return Ok(new { received = true });
        }
        catch (InvalidPayOSWebhookException)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid payOS webhook" });
        }
        catch (CheckoutUnavailableException)
        {
            return StatusCode(503, new ProblemDetails { Title = "payOS webhook is not enabled" });
        }
        catch (Exception exception) when (exception is DbUpdateException or System.Data.Common.DbException)
        {
            // Do not ACK data that was not durably stored. Never return raw DB/provider errors.
            return StatusCode(503, new ProblemDetails { Title = "Webhook storage temporarily unavailable" });
        }
    }
}
