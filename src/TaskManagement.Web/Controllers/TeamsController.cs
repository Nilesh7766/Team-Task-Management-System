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
public class TeamsController : Controller
{
    private readonly ITeamService _teams;
    private readonly IUserService _users;
    private readonly ITaskService _tasks;

    public TeamsController(ITeamService teams, IUserService users, ITaskService tasks)
    {
        _teams = teams;
        _users = users;
        _tasks = tasks;
    }

    public async Task<IActionResult> Index()
        => View(await _teams.GetTeamsAsync(User.GetUserId(), User.GetRole()));

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.GetUserId();
        var role = User.GetRole();

        try
        {
            var team = await _teams.GetTeamAsync(id, userId, role);
            var canManage = role == UserRole.Admin || (role == UserRole.Manager && team.ManagerId == userId);

            var model = new TeamDetailsViewModel
            {
                Team = team,
                Tasks = await _tasks.GetTasksAsync(userId, role, new TaskFilter { TeamId = id }),
                CanManageMembers = canManage,
                AddableUsers = canManage
                    ? (await _users.GetAllAsync())
                        .Where(u => u.TeamId != id)
                        .Select(u => new SelectListItem($"{u.FullName} ({u.Role})", u.Id.ToString()))
                        .ToList()
                    : new List<SelectListItem>()
            };

            return View(model);
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenException)
        {
            return Deny(ex);
        }
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create()
        => View(new TeamFormViewModel { Managers = await ManagerOptionsAsync() });

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeamFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Managers = await ManagerOptionsAsync();
            return View(model);
        }

        try
        {
            var created = await _teams.CreateAsync(new CreateTeamRequest
            {
                Name = model.Name,
                Description = model.Description,
                ManagerId = model.ManagerId
            }, User.GetRole());

            TempData["Success"] = $"Team \"{created.Name}\" created.";
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Managers = await ManagerOptionsAsync();
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOrAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var team = await _teams.GetTeamAsync(id, User.GetUserId(), User.GetRole());

            return View(new TeamFormViewModel
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                ManagerId = team.ManagerId,
                Managers = await ManagerOptionsAsync()
            });
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenException)
        {
            return Deny(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TeamFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Managers = await ManagerOptionsAsync();
            return View(model);
        }

        try
        {
            await _teams.UpdateAsync(model.Id, new UpdateTeamRequest
            {
                Name = model.Name,
                Description = model.Description,
                ManagerId = model.ManagerId
            }, User.GetUserId(), User.GetRole());

            TempData["Success"] = "Team saved.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Managers = await ManagerOptionsAsync();
            return View(model);
        }
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, int userId)
    {
        try
        {
            await _users.AssignTeamAsync(userId, id, User.GetUserId(), User.GetRole());
            TempData["Success"] = "Member added to the team.";
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
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        try
        {
            await _users.AssignTeamAsync(userId, null, User.GetUserId(), User.GetRole());
            TempData["Success"] = "Member removed from the team.";
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _teams.DeleteAsync(id, User.GetRole());
            TempData["Success"] = "Team deleted.";
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> ManagerOptionsAsync()
    {
        var admins = await _users.GetAllAsync(UserRole.Admin);
        var managers = await _users.GetAllAsync(UserRole.Manager);

        return managers.Concat(admins)
            .Select(u => new SelectListItem($"{u.FullName} ({u.Role})", u.Id.ToString()))
            .ToList();
    }

    private IActionResult Deny(Exception ex)
    {
        ViewBag.Code = ex is NotFoundException ? 404 : 403;
        ViewBag.Message = ex.Message;
        return View("Error");
    }
}
