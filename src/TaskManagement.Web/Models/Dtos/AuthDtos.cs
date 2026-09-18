using System.ComponentModel.DataAnnotations;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Models.Dtos;

public class RegisterRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100), StrongPassword]
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional. Only an Admin token may create Admin or Manager accounts through the API.</summary>
    public UserRole? Role { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsActive { get; set; }
}

public class ChangeRoleRequest
{
    [Required]
    public UserRole Role { get; set; }
}

public class AssignTeamRequest
{
    public int? TeamId { get; set; }
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, StrongPassword]
    public string NewPassword { get; set; } = string.Empty;
}
