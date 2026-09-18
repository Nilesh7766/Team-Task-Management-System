using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

/// <summary>Application roles used for role based access control.</summary>
public enum UserRole
{
    [Display(Name = "Admin")] Admin = 1,
    [Display(Name = "Manager")] Manager = 2,
    [Display(Name = "User")] User = 3
}

/// <summary>Lifecycle of a task. Named TaskState so it does not clash with System.Threading.Tasks.TaskStatus.</summary>
public enum TaskState
{
    [Display(Name = "To Do")] ToDo = 0,
    [Display(Name = "In Progress")] InProgress = 1,
    [Display(Name = "Done")] Done = 2
}

public enum TaskPriority
{
    [Display(Name = "Low")] Low = 0,
    [Display(Name = "Medium")] Medium = 1,
    [Display(Name = "High")] High = 2
}

public enum NotificationType
{
    [Display(Name = "Task assigned")] TaskAssigned = 0,
    [Display(Name = "Status updated")] TaskStatusUpdated = 1,
    [Display(Name = "New comment")] TaskCommented = 2,
    [Display(Name = "Team updated")] TeamMembershipChanged = 3,
    [Display(Name = "Account created")] AccountCreated = 4,
    [Display(Name = "Password reset requested")] PasswordResetRequested = 5,
    [Display(Name = "Password changed")] PasswordChanged = 6
}
