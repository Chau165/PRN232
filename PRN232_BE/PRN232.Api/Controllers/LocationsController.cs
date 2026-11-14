using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/locations")]
public sealed class LocationsController : ControllerBase
{
    private readonly IAdministrativeDivisionCatalog _catalog;

    public LocationsController(IAdministrativeDivisionCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(IReadOnlyList<AdministrativeProvinceDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AdministrativeProvinceDto>> GetProvinces()
    {
        var provinces = _catalog.GetProvinces()
            .Select(province => new AdministrativeProvinceDto(province.Code, province.Name))
            .ToArray();
        return Ok(provinces);
    }

    [HttpGet("wards")]
    [ProducesResponseType(typeof(IReadOnlyList<AdministrativeWardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<AdministrativeWardDto>> GetWards(
        [FromQuery] string? provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode) || _catalog.GetProvince(provinceCode) is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid province code",
                Detail = "Chọn mã tỉnh/thành phố có trong danh mục hành chính hiện hành."
            });
        }

        var wards = _catalog.GetCommunes(provinceCode)
            .Select(commune => new AdministrativeWardDto(commune.Code, commune.Name))
            .ToArray();
        return Ok(wards);
    }
}
