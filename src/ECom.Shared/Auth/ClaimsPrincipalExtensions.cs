namespace ECom.Shared.Auth;

using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Returns the authenticated user's id (JWT <c>sub</c>), or <see cref="Guid.Empty"/> when absent.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }
}
