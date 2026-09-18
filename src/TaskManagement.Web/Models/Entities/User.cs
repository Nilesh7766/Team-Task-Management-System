using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public bool IsActive { get; set; } = true;

    /// <summary>SHA-256 hash of the current password-reset token, if a reset was requested. Null once used or expired.</summary>
    public string? PasswordResetTokenHash { get; set; }

    /// <summary>When the current reset token stops being valid.</summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Team> ManagedTeams { get; set; } = new List<Team>();
}
