using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Models.ViewModels;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ITaskService _tasks;
    private readonly ITeamService _teams;
    private readonly INotificationService _notifications;

    public DashboardController(ITaskService tasks, ITeamService teams, INotificationService notifications)
    {
        _tasks = tasks;
        _teams = teams;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        var role = User.GetRole();

        var summary = await _tasks.GetStatusSummaryAsync(userId, role);
        var all = await _tasks.GetTasksAsync(userId, role, new TaskFilter());

        var model = new DashboardViewModel
        {
            DisplayName = User.GetDisplayName(),
            Role = role,
            ToDoCount = summary[TaskState.ToDo],
            InProgressCount = summary[TaskState.InProgress],
            DoneCount = summary[TaskState.Done],
            OverdueCount = all.Count(t => t.IsOverdue),
            ToDoTasks = all.Where(t => t.Status == TaskState.ToDo.ToString()).ToList(),
            InProgressTasks = all.Where(t => t.Status == TaskState.InProgress.ToString()).ToList(),
            DoneTasks = all.Where(t => t.Status == TaskState.Done.ToString()).ToList(),
            OverdueTasks = all.Where(t => t.IsOverdue).ToList(),
            DueSoon = all
                .Where(t => t.Status != TaskState.Done.ToString() && t.DueDate.HasValue)
                .OrderBy(t => t.DueDate)
                .Take(6)
                .ToList(),
            RecentlyUpdated = all
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .Take(5)
                .ToList(),
            Teams = await _teams.GetTeamsAsync(userId, role),
            Notifications = (await _notifications.GetForUserAsync(userId)).Take(5).ToList()
        };

        return View(model);
    }
}
