using System.Security.Claims;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }

    public static UserRole GetRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(value, out var role) ? role : UserRole.User;
    }

    public static string GetDisplayName(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Name) ?? "User";
}
