using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.ViewModels;
using TaskManagement.Web.Services;

namespace TaskManagement.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly INotificationService _notifications;

    public AccountController(IAuthService auth, INotificationService notifications)
    {
        _auth = auth;
        _notifications = notifications;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await _auth.ValidateCredentialsAsync(model.Email, model.Password);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false });

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _auth.RegisterAsync(new RegisterRequest
            {
                FullName = model.FullName,
                Email = model.Email,
                Password = model.Password
            });

            TempData["Success"] = "Account created. Sign in to get started.";
            return RedirectToAction(nameof(Login));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        ViewBag.Code = 403;
        ViewBag.Message = "Your role does not have access to that page.";
        return View("Error");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (user, token) = await _auth.GeneratePasswordResetTokenAsync(model.Email);

        if (user is not null && token is not null)
        {
            var resetLink = Url.Action(nameof(ResetPassword), "Account",
                new { email = user.Email, token }, Request.Scheme)!;

            await _notifications.NotifyPasswordResetRequestedAsync(user, resetLink);
        }

        // Same message either way, so we never reveal whether an email is registered.
        TempData["Success"] = "If that email address is registered, a password reset link has been sent to it.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return RedirectToAction(nameof(ForgotPassword));

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _auth.ResetPasswordAsync(model.Email, model.Token, model.NewPassword);
            TempData["Success"] = "Your password has been changed. Please sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
