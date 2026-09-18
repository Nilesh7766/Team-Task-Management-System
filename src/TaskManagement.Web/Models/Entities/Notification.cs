using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Models.Entities;

public class Notification
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(600)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? TaskItemId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
