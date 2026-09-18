using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

public class Comment
{
    public int Id { get; set; }

    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
