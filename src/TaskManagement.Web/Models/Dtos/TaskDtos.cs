using System.ComponentModel.DataAnnotations;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Models.Dtos;

public class CreateTaskRequest
{
    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public TaskState Status { get; set; } = TaskState.ToDo;

    public DateTime? DueDate { get; set; }

    public int? AssignedToUserId { get; set; }

    public int? TeamId { get; set; }
}

public class UpdateTaskRequest : CreateTaskRequest { }

public class UpdateStatusRequest
{
    [Required]
    public TaskState Status { get; set; }
}

public class TaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public int? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int CommentCount { get; set; }
}

public class CommentDto
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateCommentRequest
{
    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}

/// <summary>Filters used by both the dashboard and GET /api/tasks.</summary>
public class TaskFilter
{
    public TaskState? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public int? AssignedToUserId { get; set; }
    public int? TeamId { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public string? Search { get; set; }
    public bool OnlyOverdue { get; set; }
}
