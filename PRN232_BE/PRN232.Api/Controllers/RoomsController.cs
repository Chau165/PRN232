using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    // Public browsing: GET /api/rooms?status=Available&city=HCM&maxPrice=3000000
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAll(
        [FromQuery] int? propertyId,
        [FromQuery] string? status,
        [FromQuery] string? city,
        [FromQuery] decimal? maxPrice) =>
        Ok(await _roomService.GetAllAsync(propertyId, status, city, maxPrice));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> GetById(int id)
    {
        var room = await _roomService.GetByIdAsync(id);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<RoomDto>> Create(CreateRoomDto dto)
    {
        try
        {
            var created = await _roomService.CreateAsync(dto, CurrentUserId(), IsAdmin());
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.RoomId
            }, created);
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (RoomBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room", Detail = exception.Message });
        }
        catch (RoomTypeNotFoundException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room type", Detail = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<RoomDto>> Update(int id, UpdateRoomDto dto)
    {
        try
        {
            var updated = await _roomService.UpdateAsync(id, dto, CurrentUserId(), IsAdmin());
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (RoomBusinessRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room", Detail = exception.Message });
        }
        catch (RoomTypeNotFoundException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid room type", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _roomService.DeleteAsync(id, CurrentUserId(), IsAdmin()) ? NoContent() : NotFound();
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (RoomHasDependentsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Room has dependents", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}/images")]
    public async Task<ActionResult<IEnumerable<RoomImageDto>>> GetImages(int id)
    {
        var images = await _roomService.GetImagesAsync(id);
        return images is null ? NotFound() : Ok(images);
    }

    [HttpPost("{id:int}/images")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<RoomImageDto>> AddImage(int id, AddRoomImageDto dto)
    {
        try
        {
            var image = await _roomService.AddImageAsync(id, dto, CurrentUserId(), IsAdmin());
            return image is null ? NotFound() : Ok(image);
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (RoomValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RemoveImage(int id, int imageId)
    {
        try
        {
            return await _roomService.RemoveImageAsync(id, imageId, CurrentUserId(), IsAdmin()) ? NoContent() : NotFound();
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
    }

    [HttpGet("{id:int}/amenities")]
    public async Task<ActionResult<IEnumerable<AmenityDto>>> GetAmenities(int id)
    {
        var amenities = await _roomService.GetAmenitiesAsync(id);
        return amenities is null ? NotFound() : Ok(amenities);
    }

    [HttpPost("{id:int}/amenities/{amenityId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddAmenity(int id, int amenityId)
    {
        try
        {
            await _roomService.AddAmenityAsync(id, amenityId, CurrentUserId(), IsAdmin());
            return NoContent();
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
        catch (RoomNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Room not found", Detail = exception.Message });
        }
        catch (RoomAmenityNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Amenity not found", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:int}/amenities/{amenityId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RemoveAmenity(int id, int amenityId)
    {
        try
        {
            return await _roomService.RemoveAmenityAsync(id, amenityId, CurrentUserId(), IsAdmin()) ? NoContent() : NotFound();
        }
        catch (RoomForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Not the owner", Detail = exception.Message });
        }
    }

    private int CurrentUserId() =>
        int.Parse(User.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Token thiếu claim 'sub'."));

    private bool IsAdmin() => User.IsInRole("Admin");
}
