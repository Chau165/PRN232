using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;

namespace PRN232.Api.Controllers;

// Rebuild 3.5b/3.5c: ảnh/PDF minh chứng của hợp đồng (ảnh nhận phòng, ảnh trả phòng, hợp đồng đã ký).
[ApiController]
[Route("api/contracts/{contractId:int}/evidence")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContractEvidenceController(IContractEvidenceService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<ContractEvidenceDto>> Get(int contractId, CancellationToken cancellationToken) =>
        Run((userId, role) => service.GetAsync(contractId, userId, role, cancellationToken));

    // Mỗi lần một file; FE upload lần lượt để hiện tiến độ và lỗi từng file.
    [HttpPost]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ContractEvidenceService.PdfMaxBytes + 1024 * 1024)]
    public async Task<ActionResult<ContractEvidenceDto>> Upload(int contractId, [FromQuery] string kind, IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > ContractEvidenceService.PdfMaxBytes)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid evidence", Detail = "Ảnh tối đa 5 MB, PDF tối đa 10 MB." });
        }
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, cancellationToken);
        var upload = new ContractEvidenceUpload(file.FileName, file.ContentType, memory.ToArray());
        return await Run((userId, role) => service.UploadAsync(contractId, kind, upload, userId, role, cancellationToken));
    }

    [HttpDelete("{fileId:int}")]
    [Authorize(Roles = RoleNames.Customer + "," + RoleNames.Manager)]
    public Task<ActionResult<ContractEvidenceDto>> Delete(int contractId, int fileId, CancellationToken cancellationToken) =>
        Run((userId, role) => service.DeleteAsync(contractId, fileId, userId, role, cancellationToken));

    [HttpPost("{fileId:int}/hide")]
    [Authorize(Roles = RoleNames.Admin)]
    public Task<ActionResult<ContractEvidenceDto>> Hide(int contractId, int fileId, HideContractEvidenceDto dto,
        CancellationToken cancellationToken) =>
        Run((userId, role) => service.HideAsync(contractId, fileId, dto, userId, role, cancellationToken));

    private async Task<ActionResult<ContractEvidenceDto>> Run(Func<int, string, Task<ContractEvidenceDto>> action)
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
            return BadRequest(new ProblemDetails { Title = "Invalid evidence", Detail = exception.Message });
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
            return Conflict(new ProblemDetails { Title = "Evidence conflict", Detail = exception.Message });
        }
    }
}
