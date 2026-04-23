using System.Security.Claims;
namespace TaskFlowPro.API.Helpers
{
    public static class ClaimsHelper
    {
        public static Guid GetUserId(ClaimsPrincipal user)
        {
            var claim = user.FindFirst("userId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
        public static string GetUserName(ClaimsPrincipal user) =>
            user.FindFirst("name")?.Value ?? string.Empty;
        public static string GetUserRole(ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }
}