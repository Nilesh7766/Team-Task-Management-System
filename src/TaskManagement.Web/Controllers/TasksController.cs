using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Models.ViewModels;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly ITaskService _tasks;
    private readonly ITeamService _teams;
    private readonly IUserService _users;

    public TasksController(ITaskService tasks, ITeamService teams, IUserService users)
    {
        _tasks = tasks;
        _teams = teams;
        _users = users;
    }

    public async Task<IActionResult> Index([FromQuery] TaskFilter filter)
    {
        var userId = User.GetUserId();
        var role = User.GetRole();

        var model = new TaskListViewModel
        {
            Filter = filter,
            Tasks = await _tasks.GetTasksAsync(userId, role, filter),
            AssignableUsers = await BuildUserOptionsAsync(userId, role),
            Teams = await BuildTeamOptionsAsync(userId, role),
            CanCreate = role != UserRole.User
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.GetUserId();
        var role = User.GetRole();

        try
        {
            var task = await _tasks.GetTaskAsync(id, userId, role);
            var model = new TaskDetailsViewModel
            {
                Task = task,
                Comments = await _tasks.GetCommentsAsync(id, userId, role),
                CanEdit = role != UserRole.User,
                CanChangeStatus = role != UserRole.User || task.AssignedToUserId == userId
            };
            return View(model);
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenException)
        {
            return Deny(ex);
        }
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOrAdmin")]
    public async Task<IActionResult> Create()
    {
        var model = new TaskFormViewModel
        {
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            AssignableUsers = await BuildUserOptionsAsync(User.GetUserId(), User.GetRole()),
            Teams = await BuildTeamOptionsAsync(User.GetUserId(), User.GetRole())
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskFormViewModel model)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model);

        try
        {
            var created = await _tasks.CreateAsync(new CreateTaskRequest
            {
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                Priority = model.Priority,
                DueDate = model.DueDate,
                AssignedToUserId = model.AssignedToUserId,
                TeamId = model.TeamId
            }, User.GetUserId(), User.GetRole());

            TempData["Success"] = $"Task \"{created.Title}\" created.";
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RedisplayAsync(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var task = await _tasks.GetTaskAsync(id, User.GetUserId(), User.GetRole());

            var model = new TaskFormViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Status = Enum.Parse<TaskState>(task.Status),
                Priority = Enum.Parse<TaskPriority>(task.Priority),
                DueDate = task.DueDate,
                AssignedToUserId = task.AssignedToUserId,
                TeamId = task.TeamId
            };

            return await RedisplayAsync(model);
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenException)
        {
            return Deny(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaskFormViewModel model)
    {
        if (!ModelState.IsValid) return await RedisplayAsync(model);

        try
        {
            await _tasks.UpdateAsync(model.Id, new UpdateTaskRequest
            {
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                Priority = model.Priority,
                DueDate = model.DueDate,
                AssignedToUserId = model.AssignedToUserId,
                TeamId = model.TeamId
            }, User.GetUserId(), User.GetRole());

            TempData["Success"] = "Task saved.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RedisplayAsync(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TaskState status, string? returnUrl = null)
    {
        try
        {
            await _tasks.UpdateStatusAsync(id, status, User.GetUserId(), User.GetRole());
            TempData["Success"] = "Status updated and everyone involved has been notified.";
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string newComment)
    {
        try
        {
            await _tasks.AddCommentAsync(id, newComment, User.GetUserId(), User.GetRole());
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _tasks.DeleteAsync(id, User.GetUserId(), User.GetRole());
            TempData["Success"] = "Task deleted.";
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------- helpers ----------

    private async Task<IActionResult> RedisplayAsync(TaskFormViewModel model)
    {
        model.AssignableUsers = await BuildUserOptionsAsync(User.GetUserId(), User.GetRole());
        model.Teams = await BuildTeamOptionsAsync(User.GetUserId(), User.GetRole());
        return View(model.IsEdit ? "Edit" : "Create", model);
    }

    private async Task<List<SelectListItem>> BuildUserOptionsAsync(int userId, UserRole role)
    {
        var users = await _users.GetAssignableUsersAsync(userId, role);
        return users
            .Select(u => new SelectListItem($"{u.FullName} ({u.Role})", u.Id.ToString()))
            .ToList();
    }

    private async Task<List<SelectListItem>> BuildTeamOptionsAsync(int userId, UserRole role)
    {
        var teams = await _teams.GetTeamsAsync(userId, role);
        return teams.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();
    }

    private IActionResult Deny(Exception ex)
    {
        ViewBag.Code = ex is NotFoundException ? 404 : 403;
        ViewBag.Message = ex.Message;
        return View("Error");
    }
}
