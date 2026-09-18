using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers.Api;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TasksApiController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksApiController(ITaskService tasks) => _tasks = tasks;

    /// <summary>Lists tasks visible to the caller, filtered by status, priority, team, assignee or deadline.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TaskDto>>> GetAll([FromQuery] TaskFilter filter)
        => Ok(await _tasks.GetTasksAsync(User.GetUserId(), User.GetRole(), filter));

    /// <summary>Counts of To Do, In Progress and Done for the caller's dashboard.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var summary = await _tasks.GetStatusSummaryAsync(User.GetUserId(), User.GetRole());
        return Ok(summary.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Get(int id)
        => Ok(await _tasks.GetTaskAsync(id, User.GetUserId(), User.GetRole()));

    /// <summary>Creates a task. Managers and admins only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskRequest request)
    {
        var created = await _tasks.CreateAsync(request, User.GetUserId(), User.GetRole());
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskDto>> Update(int id, [FromBody] UpdateTaskRequest request)
        => Ok(await _tasks.UpdateAsync(id, request, User.GetUserId(), User.GetRole()));

    /// <summary>Moves a task between To Do, In Progress and Done. The assignee may call this on their own task.</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TaskDto>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        => Ok(await _tasks.UpdateStatusAsync(id, request.Status, User.GetUserId(), User.GetRole()));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Delete(int id)
    {
        await _tasks.DeleteAsync(id, User.GetUserId(), User.GetRole());
        return NoContent();
    }

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<List<CommentDto>>> GetComments(int id)
        => Ok(await _tasks.GetCommentsAsync(id, User.GetUserId(), User.GetRole()));

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(int id, [FromBody] CreateCommentRequest request)
    {
        var comment = await _tasks.AddCommentAsync(id, request.Text, User.GetUserId(), User.GetRole());
        return CreatedAtAction(nameof(GetComments), new { id }, comment);
    }
}
