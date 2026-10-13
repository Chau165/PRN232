using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AmenitiesController : ControllerBase
{
    private readonly IAmenityService _amenityService;

    public AmenitiesController(IAmenityService amenityService)
    {
        _amenityService = amenityService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AmenityDto>>> GetAll() => Ok(await _amenityService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AmenityDto>> GetById(int id)
    {
        var amenity = await _amenityService.GetByIdAsync(id);
        return amenity is null ? NotFound() : Ok(amenity);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<AmenityDto>> Create(CreateAmenityDto dto)
    {
        try
        {
            var created = await _amenityService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.AmenityId }, created);
        }
        catch (ReferenceDataValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid amenity", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<AmenityDto>> Update(int id, UpdateAmenityDto dto)
    {
        try
        {
            var updated = await _amenityService.UpdateAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ReferenceDataValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid amenity", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id) =>
        await _amenityService.DeleteAsync(id) ? NoContent() : NotFound();
}
