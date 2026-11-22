using Microsoft.Extensions.Primitives;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>
/// Common admission boundary for authenticated Runtime WebSockets. A socket is not a second
/// session type: it must present the same subject/client/lease identity as Runtime REST.
/// </summary>
public static class RuntimeSessionWebSocketAdmission
{
    /// <summary>
    /// Engineering realtime is an explicit read-only compatibility transport when it carries no
    /// Runtime Session identity at all. It neither creates nor participates in a lease/seat.
    /// Supplying either identity value opts a client into strict Runtime Session validation.
    /// </summary>
    public static bool IsLegacyEngineeringSocket(
        StringValues sessionIds,
        StringValues clientInstanceIds) =>
        sessionIds.Count == 0 && clientInstanceIds.Count == 0;

    public static async Task<RuntimeSessionLeaseValidation> ValidateAsync(
        ApiAuthorizationService security,
        SecurityPrincipal principal,
        ScadaRuntimeFacade runtime,
        StringValues sessionIds,
        StringValues clientInstanceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(runtime);

        if (sessionIds.Count != 1 ||
            clientInstanceIds.Count != 1 ||
            !Guid.TryParse(sessionIds.ToString(), out var sessionId) ||
            sessionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(clientInstanceIds.ToString()) ||
            clientInstanceIds.ToString().Trim().Length > RuntimeSessionLeaseRegistry.MaximumClientInstanceIdLength)
        {
            return RuntimeSessionLeaseValidation.Invalid("invalid-runtime-session-identity");
        }

        return await security.ValidateRuntimeSessionAsync(
            principal,
            runtime,
            sessionId,
            clientInstanceIds.ToString().Trim(),
            cancellationToken);
    }
}
