using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface INotificationService
{
    Task NotifyTaskAssignedAsync(TaskItem task, string actorName);
    Task NotifyStatusChangedAsync(TaskItem task, TaskState oldStatus, string actorName);

    Task NotifyCommentAddedAsync(
        TaskItem task,
        string actorName,
        int actorId);

    Task NotifyTeamChangedAsync(
        User member,
        Team? team,
        string actorName);

    Task NotifyAccountCreatedAsync(User user);

    Task NotifyPasswordResetRequestedAsync(User user, string resetLink);

    Task NotifyPasswordChangedAsync(User user);

    Task<List<NotificationDto>> GetForUserAsync(
        int userId,
        bool onlyUnread = false);

    Task<int> UnreadCountAsync(int userId);

    Task MarkReadAsync(
        int notificationId,
        int userId);

    Task MarkAllReadAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public NotificationService(
        AppDbContext db,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    // ============================================================
    // TASK ASSIGNMENT NOTIFICATION
    // ============================================================

    public async Task NotifyTaskAssignedAsync(
        TaskItem task,
        string actorName)
    {
        if (!task.AssignedToUserId.HasValue)
            return;

        var dueDate = task.DueDate.HasValue
            ? task.DueDate.Value.ToString("dd MMM yyyy")
            : "No deadline";

        var title = $"New Task Assigned: {task.Title}";

        var message =
            $"{actorName} assigned you the task \"{task.Title}\". " +
            $"Priority: {task.Priority}. " +
            $"Due Date: {dueDate}.";

        await CreateAsync(
            task.AssignedToUserId.Value,
            NotificationType.TaskAssigned,
            title,
            message,
            task.Id);
    }

    // ============================================================
    // TASK STATUS UPDATE NOTIFICATION
    // ============================================================

    public async Task NotifyStatusChangedAsync(
        TaskItem task,
        TaskState oldStatus,
        string actorName)
    {
        var recipients = new List<int>();

        // Assigned user
        if (task.AssignedToUserId.HasValue)
        {
            recipients.Add(task.AssignedToUserId.Value);
        }

        // Task creator
        recipients.Add(task.CreatedByUserId);

        // Team manager
        if (task.TeamId.HasValue)
        {
            var managerId = await _db.Teams
                .Where(t => t.Id == task.TeamId.Value)
                .Select(t => t.ManagerId)
                .FirstOrDefaultAsync();

            if (managerId.HasValue)
            {
                recipients.Add(managerId.Value);
            }
        }

        // Remove duplicate users
        recipients = recipients
            .Distinct()
            .ToList();

        foreach (var userId in recipients)
        {
            var title =
                $"Task Status Updated: {task.Title}";

            var message =
                $"{actorName} changed the status of " +
                $"\"{task.Title}\" from " +
                $"{GetStatusLabel(oldStatus)} to " +
                $"{GetStatusLabel(task.Status)}.";

            await CreateAsync(
                userId,
                NotificationType.TaskStatusUpdated,
                title,
                message,
                task.Id);
        }
    }

    // ============================================================
    // COMMENT NOTIFICATION
    // ============================================================

    public async Task NotifyCommentAddedAsync(
        TaskItem task,
        string actorName,
        int actorId)
    {
        var recipients = new List<int>();

        if (task.AssignedToUserId.HasValue)
        {
            recipients.Add(task.AssignedToUserId.Value);
        }

        recipients.Add(task.CreatedByUserId);

        foreach (var userId in recipients
                     .Distinct()
                     .Where(id => id != actorId))
        {
            await CreateAsync(
                userId,
                NotificationType.TaskCommented,
                $"New Comment: {task.Title}",
                $"{actorName} added a comment to \"{task.Title}\".",
                task.Id);
        }
    }

    // ============================================================
    // TEAM CHANGE
    // ============================================================

    public async Task NotifyTeamChangedAsync(
        User member,
        Team? team,
        string actorName)
    {
        var message = team == null
            ? $"{actorName} removed you from your team."
            : $"{actorName} added you to {team.Name}.";

        await CreateAsync(
            member.Id,
            NotificationType.TeamMembershipChanged,
            "Team Updated",
            message,
            null);
    }

    // ============================================================
    // ACCOUNT CREATED
    // ============================================================

    public async Task NotifyAccountCreatedAsync(User user)
    {
        await CreateAsync(
            user.Id,
            NotificationType.AccountCreated,
            "Welcome to Task Management",
            $"Hi {user.FullName}, your account " +
            $"({user.Email}) has been created successfully.",
            null);
    }

    // ============================================================
    // PASSWORD RESET REQUESTED
    // ============================================================

    public async Task NotifyPasswordResetRequestedAsync(User user, string resetLink)
    {
        await CreateAsync(
            user.Id,
            NotificationType.PasswordResetRequested,
            "Reset your password",
            $"Hi {user.FullName}, we received a request to reset your password. " +
            $"Click this link to choose a new one (valid for 30 minutes): {resetLink} " +
            $"If you did not request this, you can ignore this email.",
            null);
    }

    // ============================================================
    // PASSWORD CHANGED
    // ============================================================

    public async Task NotifyPasswordChangedAsync(User user)
    {
        await CreateAsync(
            user.Id,
            NotificationType.PasswordChanged,
            "Your password was changed",
            $"Hi {user.FullName}, your password for {user.Email} was just changed. " +
            $"If this wasn't you, contact an administrator immediately.",
            null);
    }

    // ============================================================
    // GET NOTIFICATIONS
    // ============================================================

    // Account/security events (account created, password reset, password changed) always
    // send an email, but they are not "work" notifications, so they are kept out of the
    // in-app notifications list and its unread count - only task/team items show up there.
    private static readonly NotificationType[] HiddenFromInAppList =
    {
        NotificationType.AccountCreated,
        NotificationType.PasswordResetRequested,
        NotificationType.PasswordChanged
    };

    public async Task<List<NotificationDto>> GetForUserAsync(
        int userId,
        bool onlyUnread = false)
    {
        var hiddenTypes = HiddenFromInAppList;

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !hiddenTypes.Contains(n.Type));

        if (onlyUnread)
        {
            query = query.Where(n => !n.IsRead);
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        return items.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type.ToString(),
            TaskItemId = n.TaskItemId,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    // ============================================================
    // UNREAD COUNT
    // ============================================================

    public Task<int> UnreadCountAsync(int userId)
    {
        var hiddenTypes = HiddenFromInAppList;

        return _db.Notifications
            .CountAsync(n =>
                n.UserId == userId &&
                !n.IsRead &&
                !hiddenTypes.Contains(n.Type));
    }

    // ============================================================
    // MARK READ
    // ============================================================

    public async Task MarkReadAsync(
        int notificationId,
        int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n =>
                n.Id == notificationId &&
                n.UserId == userId);

        if (notification == null)
            return;

        notification.IsRead = true;

        await _db.SaveChangesAsync();
    }

    // ============================================================
    // MARK ALL READ
    // ============================================================

    public async Task MarkAllReadAsync(int userId)
    {
        var notifications = await _db.Notifications
            .Where(n =>
                n.UserId == userId &&
                !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _db.SaveChangesAsync();
    }

    // ============================================================
    // CREATE IN-APP NOTIFICATION + EMAIL / MOCK
    // ============================================================

    private async Task CreateAsync(
        int userId,
        NotificationType type,
        string title,
        string message,
        int? taskId)
    {
        // ---------------------------------------
        // 1. Save in-app notification
        // ---------------------------------------

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            TaskItemId = taskId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync();

        // ---------------------------------------
        // 2. Get recipient email
        // ---------------------------------------

        var email = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning(
                "Notification created but user {UserId} has no email.",
                userId);

            return;
        }

        // ---------------------------------------
        // 3. Send Email / Mock
        // ---------------------------------------

        await SendEmailAsync(
            email,
            title,
            message);
    }

    // ============================================================
    // EMAIL / MOCK EMAIL
    // ============================================================

    private async Task SendEmailAsync(
        string to,
        string subject,
        string body)
    {
        var mode =
            _config["Notifications:Mode"] ?? "Mock";

        // ========================================================
        // MOCK MODE
        // ========================================================

        if (!string.Equals(
                mode,
                "Smtp",
                StringComparison.OrdinalIgnoreCase))
        {
            var line =
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] " +
                $"TO: {to} | " +
                $"SUBJECT: {subject} | " +
                $"BODY: {body}";

            _logger.LogInformation(
                "MOCK EMAIL {Line}",
                line);

            await AppendToLogFileAsync(line);

            return;
        }

        // ========================================================
        // SMTP MODE
        // ========================================================

        try
        {
            var smtp =
                _config.GetSection("Notifications:Smtp");

            var host = smtp["Host"];

            var port = int.TryParse(
                smtp["Port"],
                out var smtpPort)
                ? smtpPort
                : 587;

            var enableSsl = bool.TryParse(
                smtp["EnableSsl"],
                out var ssl)
                ? ssl
                : true;

            var username = smtp["UserName"];
            var password = smtp["Password"];

            var fromAddress = smtp["FromAddress"];
            var fromName = smtp["FromName"];

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromAddress))
            {
                _logger.LogError(
                    "SMTP configuration is incomplete.");

                return;
            }

            using var client =
                new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials =
                        new NetworkCredential(
                            username,
                            password)
                };

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    fromAddress,
                    fromName),

                Subject = subject,

                Body = body,

                IsBodyHtml = false
            };

            mail.To.Add(to);

            await client.SendMailAsync(mail);

            _logger.LogInformation(
                "Email sent successfully to {Recipient}.",
                to);
        }
        catch (Exception ex)
        {
            // Email failure must NOT fail task operation.
            _logger.LogError(
                ex,
                "Email to {Recipient} could not be sent.",
                to);
        }
    }

    // ============================================================
    // MOCK LOG FILE
    // ============================================================

    private async Task AppendToLogFileAsync(string line)
    {
        var path =
            _config["Notifications:LogFile"];

        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath =
            Path.Combine(
                AppContext.BaseDirectory,
                path);

        await FileLock.WaitAsync();

        try
        {
            var directory =
                Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(
                fullPath,
                line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Notification log file could not be written.");
        }
        finally
        {
            FileLock.Release();
        }
    }

    // ============================================================
    // STATUS LABEL
    // ============================================================

    private static string GetStatusLabel(TaskState state)
    {
        return state switch
        {
            TaskState.ToDo => "To Do",
            TaskState.InProgress => "In Progress",
            TaskState.Done => "Done",
            _ => state.ToString()
        };
    }
}