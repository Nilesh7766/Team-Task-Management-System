using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers.Api;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsApiController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsApiController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetMine([FromQuery] bool unreadOnly = false)
        => Ok(await _notifications.GetForUserAsync(User.GetUserId(), unreadOnly));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
        => Ok(new { count = await _notifications.UnreadCountAsync(User.GetUserId()) });

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _notifications.MarkReadAsync(id, User.GetUserId());
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(User.GetUserId());
        return NoContent();
    }
}
