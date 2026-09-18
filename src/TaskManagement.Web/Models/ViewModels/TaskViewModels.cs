using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Models.ViewModels;

public class TaskFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Give the task a title.")]
    [MaxLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public TaskState Status { get; set; } = TaskState.ToDo;

    [Display(Name = "Priority")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [DataType(DataType.Date)]
    [Display(Name = "Deadline")]
    public DateTime? DueDate { get; set; }

    [Display(Name = "Assigned to")]
    public int? AssignedToUserId { get; set; }

    [Display(Name = "Team")]
    public int? TeamId { get; set; }

    public List<SelectListItem> AssignableUsers { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
    public bool IsEdit => Id > 0;
}

public class TaskListViewModel
{
    public List<TaskDto> Tasks { get; set; } = new();
    public TaskFilter Filter { get; set; } = new();
    public List<SelectListItem> AssignableUsers { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
    public bool CanCreate { get; set; }
}

public class TaskDetailsViewModel
{
    public TaskDto Task { get; set; } = new();
    public List<CommentDto> Comments { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanChangeStatus { get; set; }

    [Required(ErrorMessage = "Write something before posting.")]
    [MaxLength(1000)]
    [Display(Name = "Comment")]
    public string NewComment { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int ToDoCount { get; set; }
    public int InProgressCount { get; set; }
    public int DoneCount { get; set; }
    public int OverdueCount { get; set; }
    public int TotalCount => ToDoCount + InProgressCount + DoneCount;
    public List<TaskDto> DueSoon { get; set; } = new();
    public List<TaskDto> RecentlyUpdated { get; set; } = new();
    public List<TaskDto> ToDoTasks { get; set; } = new();
    public List<TaskDto> InProgressTasks { get; set; } = new();
    public List<TaskDto> DoneTasks { get; set; } = new();
    public List<TaskDto> OverdueTasks { get; set; } = new();
    public List<TeamDto> Teams { get; set; } = new();
    public List<NotificationDto> Notifications { get; set; } = new();
}
