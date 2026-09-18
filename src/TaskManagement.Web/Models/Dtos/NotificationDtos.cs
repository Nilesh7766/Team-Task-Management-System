namespace TaskManagement.Web.Models.Dtos;

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? TaskItemId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
