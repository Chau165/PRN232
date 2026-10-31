using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/users?roleId= — Admin xem toàn bộ user (hoặc lọc theo role, ví dụ
    // roleId=2 để xem hết Manager).
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll([FromQuery] int? roleId)
    {
        return Ok(await _userService.GetAllAsync(roleId));
    }

    // GET /api/users/me — user tự xem profile của mình.
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var user = await _userService.GetByIdAsync(CurrentUserId());
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    // GET /api/users/{id} — Admin xem bất kỳ ai; user thường chỉ xem được chính mình.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        if (!User.IsInRole("Admin") && id != CurrentUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = "Bạn chỉ có thể xem thông tin của chính mình." });
        }

        var user = await _userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    // POST /api/users — Admin tạo account Manager/Admin/Customer trực tiếp (gán role
    // tuỳ ý qua RoleId), khác với /api/auth/register (công khai, luôn Customer).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto)
    {
        try
        {
            var created = await _userService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new
            {
                id = created.UserId
            }, created);
        }
        catch (UserEmailAlreadyExistsException exception)
        {
            return Conflict(new ProblemDetails { Title = "Email already registered", Detail = exception.Message });
        }
        catch (UserRoleNotFoundException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid role", Detail = exception.Message });
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid user", Detail = exception.Message });
        }
    }

    // PUT /api/users/{id}/role — Admin đổi role (ví dụ nâng Customer lên Manager).
    [HttpPut("{id:int}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateRole(int id, UpdateUserRoleDto dto)
    {
        try
        {
            var updated = await _userService.UpdateRoleAsync(id, dto, CurrentUserId());
            if (updated is null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (UserRoleNotFoundException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid role", Detail = exception.Message });
        }
        catch (UserCannotModifySelfException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot modify self", Detail = exception.Message });
        }
    }

    // PUT /api/users/{id}/status — Admin khoá/mở khoá account.
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateStatus(int id, UpdateUserStatusDto dto)
    {
        try
        {
            var updated = await _userService.UpdateStatusAsync(id, dto, CurrentUserId());
            if (updated is null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (UserCannotModifySelfException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot modify self", Detail = exception.Message });
        }
    }

    // PUT /api/users/me — user tự sửa FullName/PhoneNumber (không đổi Email/Role).
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileDto dto)
    {
        try
        {
            var updated = await _userService.UpdateProfileAsync(CurrentUserId(), dto, CurrentUserId());
            if (updated is null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid profile", Detail = exception.Message });
        }
    }

    // PUT /api/users/me/password — user tự đổi mật khẩu (phải nhập đúng mật khẩu cũ).
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        try
        {
            await _userService.ChangePasswordAsync(CurrentUserId(), dto, CurrentUserId());
            return NoContent();
        }
        catch (UserInvalidCurrentPasswordException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid current password", Detail = exception.Message });
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid password", Detail = exception.Message });
        }
    }

    private int CurrentUserId()
    {
        var subjectId = User.FindFirst("sub")?.Value;
        if (subjectId is null)
        {
            throw new InvalidOperationException("Token thiếu claim 'sub'.");
        }
        return int.Parse(subjectId);
    }
}
