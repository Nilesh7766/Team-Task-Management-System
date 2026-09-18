using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Web.Helpers;

/// <summary>
/// Central password strength rule so the web form, the API DTO, and the service layer
/// all agree on what counts as an acceptable password.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public const string RequirementsText =
        "At least 8 characters, including an uppercase letter, a lowercase letter, a digit and a special character.";

    /// <summary>Returns null when the password is strong enough, otherwise a user-facing error message.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return "Choose a password.";

        if (password.Length < MinLength)
            return $"Password must be at least {MinLength} characters long.";

        if (!password.Any(char.IsUpper))
            return "Password must contain at least one uppercase letter.";

        if (!password.Any(char.IsLower))
            return "Password must contain at least one lowercase letter.";

        if (!password.Any(char.IsDigit))
            return "Password must contain at least one digit.";

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return "Password must contain at least one special character.";

        return null;
    }

    public static bool IsValid(string? password) => Validate(password) is null;
}

/// <summary>DataAnnotations wrapper around <see cref="PasswordPolicy"/> for view models and DTOs.</summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute() : base(PasswordPolicy.RequirementsText)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var error = PasswordPolicy.Validate(value as string);
        return error is null ? ValidationResult.Success : new ValidationResult(error, new[] { validationContext.MemberName ?? "Password" });
    }
}
