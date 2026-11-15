using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
namespace PRN232.Api.Controllers;
[ApiController, Authorize(Roles="Admin"), Route("api/properties/location-preview")]
public sealed class PropertyLocationPreviewController(IPropertyLocationPreviewService service, ICurrentUserService user) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Preview(PropertyLocationPreviewRequest request, CancellationToken token)
    {
        if (!user.UserId.HasValue) return Unauthorized();
        try { return Ok(await service.PreviewAsync(request,token)); }
        catch (PropertyValidationException e) { return BadRequest(new ProblemDetails { Title="Invalid address",Detail=e.Message,Status=400 }); }
    }
}
