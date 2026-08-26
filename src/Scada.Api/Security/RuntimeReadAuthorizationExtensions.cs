using Scada.Api.Runtime;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class RuntimeReadAuthorizationExtensions
{
    public static async Task<bool> CanViewRuntimeResourceAsync(
        this ApiAuthorizationService security,
        SecurityPrincipal principal,
        ScadaRuntimeFacade runtime,
        AuthorizationResource? resource = null,
        CancellationToken cancellationToken = default)
    {
        if (!security.AuthenticationEnabled) return true;
        var check = await security.CheckRuntimeAsync(
            principal,
            runtime,
            SecurityCapability.View,
            resource,
            cancellationToken);
        return check.Allowed;
    }
}
