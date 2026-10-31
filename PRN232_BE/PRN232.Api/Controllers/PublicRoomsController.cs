using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/rooms")]
public sealed class PublicRoomsController : ControllerBase
{
    private readonly IPublicPropertyListingService _propertyListingService;

    public PublicRoomsController(IPublicPropertyListingService propertyListingService)
    {
        _propertyListingService = propertyListingService;
    }

    [HttpGet("{roomId:int}")]
    [ProducesResponseType(typeof(PublicRoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicRoomDetailDto>> GetById(
        int roomId,
        CancellationToken cancellationToken)
    {
        var room = await _propertyListingService.GetRoomDetailAsync(roomId, cancellationToken);
        if (room is null)
        {
            return NotFound();
        }
        return Ok(room);
    }
}
