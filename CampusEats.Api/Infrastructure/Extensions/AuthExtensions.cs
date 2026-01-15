using System.Security.Claims;

namespace CampusEats.Api.Infrastructure.Extensions;

public static class AuthExtensions
{
    public static Guid? GetUserId(this HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }

    public static string? GetUserRole(this HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
    }

    public static bool IsAdmin(this HttpContext httpContext)
    {
        return httpContext.GetUserRole() == "Admin";
    }

    public static bool IsManager(this HttpContext httpContext)
    {
        var role = httpContext.GetUserRole();
        return role is "Manager" or "Admin";
    }

    public static bool IsStaff(this HttpContext httpContext)
    {
        var role = httpContext.GetUserRole();
        return role is "Staff" or "Manager" or "Admin";
    }

    public static bool CanAccessUserData(this HttpContext httpContext, Guid targetUserId)
    {
        var currentUserId = httpContext.GetUserId();
        return currentUserId == targetUserId || httpContext.IsAdmin();
    }
}