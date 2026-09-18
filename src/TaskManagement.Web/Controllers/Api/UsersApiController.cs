using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers.Api;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UsersApiController : ControllerBase
{
    private readonly IUserService _users;

    public UsersApiController(IUserService users) => _users = users;

    /// <summary>Full directory. Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<List<UserDto>>> GetAll([FromQuery] UserRole? role)
        => Ok(await _users.GetAllAsync(role));

    /// <summary>People the caller is allowed to assign work to.</summary>
    [HttpGet("assignable")]
    public async Task<ActionResult<List<UserDto>>> Assignable()
        => Ok(await _users.GetAssignableUsersAsync(User.GetUserId(), User.GetRole()));

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var dto = await _users.GetAsync(id);
        return dto is null ? NotFound(new ApiError(404, "User not found.")) : Ok(dto);
    }

    [HttpPatch("{id:int}/role")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
    {
        await _users.ChangeRoleAsync(id, request.Role);
        return Ok(await _users.GetAsync(id));
    }

    [HttpPatch("{id:int}/team")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> AssignTeam(int id, [FromBody] AssignTeamRequest request)
    {
        await _users.AssignTeamAsync(id, request.TeamId, User.GetUserId(), User.GetRole());
        return Ok(await _users.GetAsync(id));
    }

    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool isActive = true)
    {
        await _users.SetActiveAsync(id, isActive);
        return Ok(await _users.GetAsync(id));
    }
}
