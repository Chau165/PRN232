using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/properties")]
public sealed class PublicPropertiesController : ControllerBase
{
    private readonly IPublicPropertyListingService _propertyListingService;

    public PublicPropertiesController(IPublicPropertyListingService propertyListingService)
    {
        _propertyListingService = propertyListingService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PublicPropertyPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicPropertyPageDto>> GetAll(
        [FromQuery] PublicPropertyQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _propertyListingService.GetAllAsync(query, cancellationToken));
        }
        catch (PublicPropertyQueryValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Field] = [exception.Message]
            }));
        }
    }

    [HttpGet("by-area")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicPropertyAreaCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PublicPropertyAreaCardDto>>> GetByArea(
        [FromQuery] PublicPropertyAreaQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _propertyListingService.GetByAreaAsync(query, cancellationToken));
        }
        catch (PublicPropertyQueryValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Field] = [exception.Message]
            }));
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublicPropertyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicPropertyDetailDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var property = await _propertyListingService.GetDetailAsync(id, cancellationToken);
        if (property is null)
        {
            return NotFound();
        }
        return Ok(property);
    }

    [HttpGet("{id:int}/rooms")]
    [ProducesResponseType(typeof(PublicPropertyRoomPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicPropertyRoomPageDto>> GetRooms(
        int id,
        [FromQuery] PublicPropertyRoomsQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _propertyListingService.GetRoomsAsync(id, query, cancellationToken);
            if (page is null)
            {
                return NotFound();
            }
            return Ok(page);
        }
        catch (PublicPropertyQueryValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Field] = [exception.Message]
            }));
        }
    }
}
