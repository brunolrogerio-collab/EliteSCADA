using System.Security.Claims;

namespace Scadivo.Portal;

public static class PortalAccess
{
    public const string AdministratorPolicy = "PortalAdministrator";
    public static bool IsAdministrator(ClaimsPrincipal principal) => principal.IsInRole("Owner") || principal.IsInRole("Admin");
}
