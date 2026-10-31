using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Controllers;

// Module mẫu: Controller -> Service -> Repository -> AppDbContext.
// Copy đúng pattern này (DTO, Interface, Service, Repository, Controller, đăng ký DI
// trong Program.cs) cho module của bạn (Property, Booking, Contract, Invoice, ...).
[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
    {
        return Ok(await _roleService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoleDto>> GetById(int id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }
        return Ok(role);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleDto dto)
    {
        var created = await _roleService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new
        {
            id = created.RoleId
        }, created);
    }
}
