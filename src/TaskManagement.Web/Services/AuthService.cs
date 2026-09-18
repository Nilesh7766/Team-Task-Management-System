using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Web.Data;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterRequest request, UserRole? callerRole = null);
    Task<User> ValidateCredentialsAsync(string email, string password);
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// If the email belongs to an active account, generates a one-time reset token (valid 30 minutes)
    /// and returns it together with the user so the caller can build a reset link and email it.
    /// Returns (null, null) when the email is unknown, so callers can show the same generic message
    /// either way and avoid revealing which emails are registered.
    /// </summary>
    Task<(User? User, string? Token)> GeneratePasswordResetTokenAsync(string email);

    /// <summary>Validates the reset token and sets the new password. Throws DomainException on any failure.</summary>
    Task ResetPasswordAsync(string email, string token, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly INotificationService _notifications;

    public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtTokenService tokens, INotificationService notifications)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _notifications = notifications;
    }

    public async Task<User> RegisterAsync(RegisterRequest request, UserRole? callerRole = null)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new DomainException("That email address is already registered.");

        // Defense in depth: DTO/view-model validation should already catch a weak password,
        // but the service never trusts a caller that skips or bypasses that validation.
        var passwordError = PasswordPolicy.Validate(request.Password);
        if (passwordError is not null)
            throw new DomainException(passwordError);

        var role = request.Role ?? UserRole.User;

        // Only an Admin may create privileged accounts. Self sign-up is always a plain User.
        if (role != UserRole.User && callerRole != UserRole.Admin)
            role = UserRole.User;

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _notifications.NotifyAccountCreatedAsync(user);

        return user;
    }

    public async Task<User> ValidateCredentialsAsync(string email, string password)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Team).FirstOrDefaultAsync(u => u.Email == normalized);

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            throw new DomainException("Email or password is incorrect.");

        if (!user.IsActive)
            throw new DomainException("This account has been deactivated. Contact an administrator.");

        return user;
    }

    public async Task<(User? User, string? Token)> GeneratePasswordResetTokenAsync(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        // Do not reveal whether an email is registered - same (null, null) result either way.
        if (user is null || !user.IsActive)
            return (null, null);

        var token = GenerateUrlSafeToken();
        user.PasswordResetTokenHash = HashToken(token);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        return (user, token);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        if (user is null || string.IsNullOrEmpty(user.PasswordResetTokenHash) || user.PasswordResetTokenExpiresAt is null)
            throw new DomainException("This password reset link is invalid or has already been used.");

        if (user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
            throw new DomainException("This password reset link has expired. Please request a new one.");

        if (string.IsNullOrEmpty(token) || HashToken(token) != user.PasswordResetTokenHash)
            throw new DomainException("This password reset link is invalid or has already been used.");

        var passwordError = PasswordPolicy.Validate(newPassword);
        if (passwordError is not null)
            throw new DomainException(passwordError);

        user.PasswordHash = _hasher.Hash(newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        await _notifications.NotifyPasswordChangedAsync(user);
    }

    private static string GenerateUrlSafeToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await ValidateCredentialsAsync(request.Email, request.Password);
        var (token, expires) = _tokens.CreateToken(user);

        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expires,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                TeamId = user.TeamId,
                TeamName = user.Team?.Name,
                IsActive = user.IsActive
            }
        };
    }
}
