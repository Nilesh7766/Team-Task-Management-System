using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers.Api;

[ApiController]
[Route("api/teams")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TeamsApiController : ControllerBase
{
    private readonly ITeamService _teams;
    private readonly IUserService _users;

    public TeamsApiController(ITeamService teams, IUserService users)
    {
        _teams = teams;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<List<TeamDto>>> GetAll()
        => Ok(await _teams.GetTeamsAsync(User.GetUserId(), User.GetRole()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TeamDto>> Get(int id)
        => Ok(await _teams.GetTeamAsync(id, User.GetUserId(), User.GetRole()));

    /// <summary>Creates a team and optionally hands it to a manager. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<TeamDto>> Create([FromBody] CreateTeamRequest request)
    {
        var created = await _teams.CreateAsync(request, User.GetRole());
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<TeamDto>> Update(int id, [FromBody] UpdateTeamRequest request)
        => Ok(await _teams.UpdateAsync(id, request, User.GetUserId(), User.GetRole()));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Delete(int id)
    {
        await _teams.DeleteAsync(id, User.GetRole());
        return NoContent();
    }

    /// <summary>Adds a user to the team. A manager may do this for their own teams.</summary>
    [HttpPost("{id:int}/members")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequest request)
    {
        await _users.AssignTeamAsync(request.UserId, id, User.GetUserId(), User.GetRole());
        return Ok(await _teams.GetTeamAsync(id, User.GetUserId(), User.GetRole()));
    }

    [HttpDelete("{id:int}/members/{userId:int}")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        await _users.AssignTeamAsync(userId, null, User.GetUserId(), User.GetRole());
        return NoContent();
    }
}
