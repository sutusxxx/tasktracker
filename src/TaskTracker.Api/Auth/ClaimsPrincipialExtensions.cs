using System.Security.Claims;

namespace TaskTracker.Api.Auth;

internal static class ClaimsPrincipialExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("sub")
               ?? throw new InvalidOperationException("Authenticated principal has no 'sub' claim.");
    }
}