using System.Globalization;
using System.Security.Claims;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public sealed record PublicRuntimeSessionIdentity(
    string SubjectId,
    Guid SessionId,
    string ClientInstanceId,
    long PolicyVersion,
    long AuthorityEpoch);

public static class PublicRuntimeSessionSecurity
{
    public const string CookieName = "scadivo.public-runtime";
    public const string MarkerClaim = "scadivo_public_runtime";
    public const string SessionIdClaim = "scadivo_public_runtime_session";
    public const string ClientInstanceClaim = "scadivo_public_runtime_client";
    public const string PolicyVersionClaim = "scadivo_public_runtime_policy_version";

    public static bool TryRead(ClaimsPrincipal principal, out PublicRuntimeSessionIdentity identity)
    {
        identity = default!;
        if (!string.Equals(principal.FindFirst(MarkerClaim)?.Value, "1", StringComparison.Ordinal) ||
            !Guid.TryParse(principal.FindFirst(SessionIdClaim)?.Value, out var sessionId) || sessionId == Guid.Empty ||
            !long.TryParse(principal.FindFirst(PolicyVersionClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var policyVersion) ||
            policyVersion < 0 ||
            !long.TryParse(principal.FindFirst(JwtTokenIssuer.AuthorityEpochClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var authorityEpoch) ||
            authorityEpoch < 1)
            return false;

        var subjectId = principal.FindFirst("sub")?.Value;
        var clientInstanceId = principal.FindFirst(ClientInstanceClaim)?.Value;
        if (string.IsNullOrWhiteSpace(subjectId) ||
            string.IsNullOrWhiteSpace(clientInstanceId) ||
            clientInstanceId.Length > Scada.Api.Runtime.RuntimeSessionLeaseRegistry.MaximumClientInstanceIdLength)
            return false;

        identity = new PublicRuntimeSessionIdentity(subjectId, sessionId, clientInstanceId, policyVersion, authorityEpoch);
        return true;
    }
}

public static class PublicRuntimeJwtSessionValidator
{
    public static async Task<bool> IsCurrentAsync(
        ClaimsPrincipal principal,
        IAuthorityLifecycleStore lifecycle,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                principal.FindFirst(JwtTokenIssuer.IdentityProviderClaim)?.Value,
                JwtTokenIssuer.PublicRuntimeIdentityProvider,
                StringComparison.Ordinal) ||
            !PublicRuntimeSessionSecurity.TryRead(principal, out var visitor))
            return false;
        return await IsCurrentAsync(visitor, lifecycle, cancellationToken);
    }

    public static async Task<bool> IsCurrentAsync(
        PublicRuntimeSessionIdentity visitor,
        IAuthorityLifecycleStore lifecycle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await lifecycle.GetAsync(cancellationToken);
            return AuthorityLifecycleSessionFence.IsCurrent(snapshot, visitor.AuthorityEpoch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
