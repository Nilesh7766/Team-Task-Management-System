using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Models.ViewModels;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UsersController : Controller
{
    private readonly IUserService _users;
    private readonly ITeamService _teams;

    public UsersController(IUserService users, ITeamService teams)
    {
        _users = users;
        _teams = teams;
    }

    public async Task<IActionResult> Index()
    {
        var teams = await _teams.GetTeamsAsync(User.GetUserId(), UserRole.Admin);

        return View(new UserListViewModel
        {
            Users = await _users.GetAllAsync(),
            Teams = teams.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, UserRole role)
    {
        try
        {
            await _users.ChangeRoleAsync(id, role);
            TempData["Success"] = "Role updated.";
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTeam(int id, int? teamId)
    {
        try
        {
            await _users.AssignTeamAsync(id, teamId, User.GetUserId(), UserRole.Admin);
            TempData["Success"] = "Team membership updated.";
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        try
        {
            await _users.SetActiveAsync(id, isActive);
            TempData["Success"] = isActive ? "Account reactivated." : "Account deactivated.";
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
