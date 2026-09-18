using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _users;
    private readonly INotificationService _notifications;

    public AuthApiController(IAuthService auth, IUserService users, INotificationService notifications)
    {
        _auth = auth;
        _users = users;
        _notifications = notifications;
    }

    /// <summary>Registers a new account. Self sign-up always creates a User; only an Admin token can set a higher role.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        UserRole? callerRole = User.Identity?.IsAuthenticated == true ? User.GetRole() : null;
        var user = await _auth.RegisterAsync(request, callerRole);
        var dto = await _users.GetAsync(user.Id);
        return CreatedAtAction(nameof(Me), null, dto);
    }

    /// <summary>Exchanges email and password for a signed JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        => Ok(await _auth.LoginAsync(request));

    /// <summary>Returns the profile behind the current token.</summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var dto = await _users.GetAsync(User.GetUserId());
        return dto is null ? NotFound(new ApiError(404, "Account not found.")) : Ok(dto);
    }

    /// <summary>
    /// Requests a password reset email. Always returns 200 with the same generic message,
    /// whether or not the email is registered, so callers cannot use this to discover accounts.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var (user, token) = await _auth.GeneratePasswordResetTokenAsync(request.Email);

        if (user is not null && token is not null)
        {
            var resetLink = Url.Action("ResetPassword", "Account",
                new { email = user.Email, token }, Request.Scheme)!;

            await _notifications.NotifyPasswordResetRequestedAsync(user, resetLink);
        }

        return Ok(new { message = "If that email address is registered, a password reset link has been sent to it." });
    }

    /// <summary>Sets a new password using the token emailed by /forgot-password.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _auth.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
        return Ok(new { message = "Password has been reset. You can now sign in with your new password." });
    }
}
