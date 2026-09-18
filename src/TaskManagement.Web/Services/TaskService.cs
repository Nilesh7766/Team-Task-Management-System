using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface ITaskService
{
    Task<List<TaskDto>> GetTasksAsync(int actorId, UserRole actorRole, TaskFilter filter);
    Task<TaskDto> GetTaskAsync(int id, int actorId, UserRole actorRole);
    Task<TaskItem> GetEntityForActorAsync(int id, int actorId, UserRole actorRole);
    Task<TaskDto> CreateAsync(CreateTaskRequest request, int actorId, UserRole actorRole);
    Task<TaskDto> UpdateAsync(int id, UpdateTaskRequest request, int actorId, UserRole actorRole);
    Task<TaskDto> UpdateStatusAsync(int id, TaskState status, int actorId, UserRole actorRole);
    Task DeleteAsync(int id, int actorId, UserRole actorRole);
    Task<List<CommentDto>> GetCommentsAsync(int taskId, int actorId, UserRole actorRole);
    Task<CommentDto> AddCommentAsync(int taskId, string text, int actorId, UserRole actorRole);
    Task<Dictionary<TaskState, int>> GetStatusSummaryAsync(int actorId, UserRole actorRole);
}

public class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public TaskService(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // ---------- Reads ----------

    public async Task<List<TaskDto>> GetTasksAsync(int actorId, UserRole actorRole, TaskFilter filter)
    {
        var query = await ScopedQueryAsync(actorId, actorRole);

        if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
        if (filter.Priority.HasValue) query = query.Where(t => t.Priority == filter.Priority.Value);
        if (filter.AssignedToUserId.HasValue) query = query.Where(t => t.AssignedToUserId == filter.AssignedToUserId.Value);
        if (filter.TeamId.HasValue) query = query.Where(t => t.TeamId == filter.TeamId.Value);
        if (filter.DueFrom.HasValue) query = query.Where(t => t.DueDate != null && t.DueDate >= filter.DueFrom.Value.Date);
        if (filter.DueTo.HasValue) query = query.Where(t => t.DueDate != null && t.DueDate <= filter.DueTo.Value.Date);

        if (filter.OnlyOverdue)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(t => t.DueDate != null && t.DueDate < today && t.Status != TaskState.Done);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(t => t.Title.Contains(term) || (t.Description != null && t.Description.Contains(term)));
        }

        var tasks = await query
            .Include(t => t.AssignedToUser)
            .Include(t => t.CreatedByUser)
            .Include(t => t.Team)
            .Include(t => t.Comments)
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<TaskDto> GetTaskAsync(int id, int actorId, UserRole actorRole)
        => ToDto(await GetEntityForActorAsync(id, actorId, actorRole));

    public async Task<TaskItem> GetEntityForActorAsync(int id, int actorId, UserRole actorRole)
    {
        var task = await _db.Tasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.CreatedByUser)
            .Include(t => t.Team)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Task {id} was not found.");

        if (!await CanViewAsync(task, actorId, actorRole))
            throw new ForbiddenException("You do not have access to this task.");

        return task;
    }

    public async Task<Dictionary<TaskState, int>> GetStatusSummaryAsync(int actorId, UserRole actorRole)
    {
        var query = await ScopedQueryAsync(actorId, actorRole);

        var grouped = await query
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var summary = new Dictionary<TaskState, int>
        {
            [TaskState.ToDo] = 0,
            [TaskState.InProgress] = 0,
            [TaskState.Done] = 0
        };

        foreach (var row in grouped) summary[row.Status] = row.Count;
        return summary;
    }

    // ---------- Writes ----------

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, int actorId, UserRole actorRole)
    {
        if (actorRole == UserRole.User)
            throw new ForbiddenException("Only managers and administrators can create tasks.");

        await ValidateAssignmentAsync(request.AssignedToUserId, request.TeamId, actorId, actorRole);

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = request.Status,
            Priority = request.Priority,
            DueDate = request.DueDate?.Date,
            AssignedToUserId = request.AssignedToUserId,
            TeamId = request.TeamId,
            CreatedByUserId = actorId,
            CompletedAt = request.Status == TaskState.Done ? DateTime.UtcNow : null
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        if (task.AssignedToUserId.HasValue)
            await _notifications.NotifyTaskAssignedAsync(task, await ActorNameAsync(actorId));

        return ToDto(await ReloadAsync(task.Id));
    }

    public async Task<TaskDto> UpdateAsync(int id, UpdateTaskRequest request, int actorId, UserRole actorRole)
    {
        var task = await GetEntityForActorAsync(id, actorId, actorRole);

        if (!await CanEditAsync(task, actorId, actorRole))
            throw new ForbiddenException("You can only edit tasks you own or manage.");

        var actorName = await ActorNameAsync(actorId);
        var previousAssignee = task.AssignedToUserId;
        var previousStatus = task.Status;

        // A plain User may move their own task along, but not rewrite or re-assign it.
        if (actorRole == UserRole.User)
        {
            task.Status = request.Status;
        }
        else
        {
            await ValidateAssignmentAsync(request.AssignedToUserId, request.TeamId, actorId, actorRole);

            task.Title = request.Title.Trim();
            task.Description = request.Description?.Trim();
            task.Priority = request.Priority;
            task.DueDate = request.DueDate?.Date;
            task.AssignedToUserId = request.AssignedToUserId;
            task.TeamId = request.TeamId;
            task.Status = request.Status;
        }

        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = task.Status == TaskState.Done ? (task.CompletedAt ?? DateTime.UtcNow) : null;

        await _db.SaveChangesAsync();

        if (task.AssignedToUserId.HasValue && task.AssignedToUserId != previousAssignee)
            await _notifications.NotifyTaskAssignedAsync(task, actorName);

        if (task.Status != previousStatus)
            await _notifications.NotifyStatusChangedAsync(task, previousStatus, actorName);

        return ToDto(await ReloadAsync(task.Id));
    }

    public async Task<TaskDto> UpdateStatusAsync(int id, TaskState status, int actorId, UserRole actorRole)
    {
        var task = await GetEntityForActorAsync(id, actorId, actorRole);

        var isAssignee = task.AssignedToUserId == actorId;
        if (!isAssignee && !await CanEditAsync(task, actorId, actorRole))
            throw new ForbiddenException("You can only change the status of your own tasks.");

        if (task.Status == status)
            return ToDto(task);

        var previous = task.Status;
        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = status == TaskState.Done ? DateTime.UtcNow : null;

        await _db.SaveChangesAsync();
        await _notifications.NotifyStatusChangedAsync(task, previous, await ActorNameAsync(actorId));

        return ToDto(await ReloadAsync(task.Id));
    }

    public async Task DeleteAsync(int id, int actorId, UserRole actorRole)
    {
        var task = await GetEntityForActorAsync(id, actorId, actorRole);

        if (actorRole == UserRole.User)
            throw new ForbiddenException("Only managers and administrators can delete tasks.");

        if (!await CanEditAsync(task, actorId, actorRole))
            throw new ForbiddenException("You can only delete tasks you own or manage.");

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
    }

    // ---------- Comments ----------

    public async Task<List<CommentDto>> GetCommentsAsync(int taskId, int actorId, UserRole actorRole)
    {
        await GetEntityForActorAsync(taskId, actorId, actorRole);

        var comments = await _db.Comments
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.TaskItemId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(c => new CommentDto
        {
            Id = c.Id,
            TaskItemId = c.TaskItemId,
            Text = c.Text,
            UserId = c.UserId,
            UserName = c.User?.FullName ?? "Unknown",
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<CommentDto> AddCommentAsync(int taskId, string text, int actorId, UserRole actorRole)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("A comment cannot be empty.");

        var task = await GetEntityForActorAsync(taskId, actorId, actorRole);

        var comment = new Comment
        {
            TaskItemId = task.Id,
            UserId = actorId,
            Text = text.Trim()
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var actorName = await ActorNameAsync(actorId);
        await _notifications.NotifyCommentAddedAsync(task, actorName, actorId);

        return new CommentDto
        {
            Id = comment.Id,
            TaskItemId = comment.TaskItemId,
            Text = comment.Text,
            UserId = actorId,
            UserName = actorName,
            CreatedAt = comment.CreatedAt
        };
    }

    // ---------- Access rules ----------

    /// <summary>
    /// Admin sees everything, a Manager sees their teams' work plus anything they created or own,
    /// a User sees only tasks assigned to them.
    /// </summary>
    private async Task<IQueryable<TaskItem>> ScopedQueryAsync(int actorId, UserRole actorRole)
    {
        var query = _db.Tasks.AsQueryable();

        if (actorRole == UserRole.Admin)
            return query;

        if (actorRole == UserRole.Manager)
        {
            var teamIds = await _db.Teams.Where(t => t.ManagerId == actorId).Select(t => t.Id).ToListAsync();
            return query.Where(t =>
                (t.TeamId != null && teamIds.Contains(t.TeamId.Value)) ||
                t.CreatedByUserId == actorId ||
                t.AssignedToUserId == actorId);
        }

        return query.Where(t => t.AssignedToUserId == actorId);
    }

    private async Task<bool> CanViewAsync(TaskItem task, int actorId, UserRole actorRole)
    {
        if (actorRole == UserRole.Admin) return true;
        if (task.AssignedToUserId == actorId || task.CreatedByUserId == actorId) return true;

        if (actorRole == UserRole.Manager && task.TeamId.HasValue)
            return await _db.Teams.AnyAsync(t => t.Id == task.TeamId && t.ManagerId == actorId);

        return false;
    }

    private async Task<bool> CanEditAsync(TaskItem task, int actorId, UserRole actorRole)
    {
        if (actorRole == UserRole.Admin) return true;

        if (actorRole == UserRole.Manager)
        {
            if (task.CreatedByUserId == actorId) return true;
            if (task.TeamId.HasValue)
                return await _db.Teams.AnyAsync(t => t.Id == task.TeamId && t.ManagerId == actorId);
            return false;
        }

        // Users may only touch the status of a task assigned to them.
        return task.AssignedToUserId == actorId;
    }

    private async Task ValidateAssignmentAsync(int? assigneeId, int? teamId, int actorId, UserRole actorRole)
    {
        if (teamId.HasValue)
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId.Value)
                       ?? throw new NotFoundException($"Team {teamId} was not found.");

            if (actorRole == UserRole.Manager && team.ManagerId != actorId)
                throw new ForbiddenException("You can only create tasks for teams you manage.");
        }

        if (!assigneeId.HasValue) return;

        var assignee = await _db.Users.FirstOrDefaultAsync(u => u.Id == assigneeId.Value)
                       ?? throw new NotFoundException($"User {assigneeId} was not found.");

        if (!assignee.IsActive)
            throw new DomainException("That user account is deactivated.");

        if (actorRole == UserRole.Manager && assignee.Id != actorId)
        {
            var managesAssignee = assignee.TeamId.HasValue &&
                await _db.Teams.AnyAsync(t => t.Id == assignee.TeamId && t.ManagerId == actorId);

            if (!managesAssignee)
                throw new ForbiddenException("You can only assign tasks to members of your own teams.");
        }
    }

    // ---------- Helpers ----------

    private async Task<TaskItem> ReloadAsync(int id)
        => await _db.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.CreatedByUser)
            .Include(t => t.Team)
            .Include(t => t.Comments)
            .FirstAsync(t => t.Id == id);

    private async Task<string> ActorNameAsync(int actorId)
        => await _db.Users.Where(u => u.Id == actorId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Someone";

    internal static TaskDto ToDto(TaskItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status.ToString(),
        Priority = t.Priority.ToString(),
        DueDate = t.DueDate,
        IsOverdue = t.IsOverdue,
        AssignedToUserId = t.AssignedToUserId,
        AssignedToName = t.AssignedToUser?.FullName,
        CreatedByUserId = t.CreatedByUserId,
        CreatedByName = t.CreatedByUser?.FullName,
        TeamId = t.TeamId,
        TeamName = t.Team?.Name,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CommentCount = t.Comments?.Count ?? 0
    };
}
