using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

public class Team
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Manager responsible for this team (assigned by an Admin).</summary>
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Members { get; set; } = new List<User>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
