using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    public async Task<IActionResult> Index(bool unreadOnly = false)
    {
        ViewBag.UnreadOnly = unreadOnly;
        var items = await _notifications.GetForUserAsync(User.GetUserId(), unreadOnly);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _notifications.MarkReadAsync(id, User.GetUserId());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(User.GetUserId());
        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }
}
