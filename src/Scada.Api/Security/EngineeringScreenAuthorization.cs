using Microsoft.AspNetCore.Http;
using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

/// <summary>
/// Resolves Engineering screen visibility against the canonical stable Authority
/// scope graph. A caller never supplies the scope identity; it is derived from
/// the persisted Screen id and enriched by ApiAuthorizationService.
/// </summary>
public static class EngineeringScreenAuthorization
{
    public static bool CanRead(
        HttpContext context,
        ApiAuthorizationService security,
        ScreenEngineeringDto screen)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(screen);

        if (!security.AuthenticationEnabled) return true;
        if (!screen.Id.HasValue || screen.Id == Guid.Empty) return false;

        return security.CheckWorkspace(
            context,
            SecurityCapability.EngineeringView,
            new AuthorizationResource(
                ScreenKey: screen.Key,
                ResourceKind: AuthorizationResourceKind.Screen,
                ResourceId: screen.Id.Value)).Allowed;
    }

    public static IReadOnlyCollection<ScreenEngineeringDto> FilterReadable(
        HttpContext context,
        ApiAuthorizationService security,
        IReadOnlyCollection<ScreenEngineeringDto> screens) =>
        screens.Where(screen => CanRead(context, security, screen)).ToArray();
}
