using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

public class TaskItem
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TaskState Status { get; set; } = TaskState.ToDo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }

    public int? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public bool IsOverdue =>
        DueDate.HasValue && Status != TaskState.Done && DueDate.Value.Date < DateTime.UtcNow.Date;
}
