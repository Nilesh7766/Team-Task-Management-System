namespace TaskManagement.Web.Models.Dtos;

/// <summary>Uniform error body returned by every API endpoint.</summary>
public class ApiError
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }

    public ApiError() { }

    public ApiError(int status, string message)
    {
        Status = status;
        Message = message;
    }
}
