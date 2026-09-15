using System.Security.Claims;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

/// <summary>Durable local-Authority fence shared by the JWT event and focused contract tests.</summary>
internal static class LocalAuthorityJwtSessionValidator
{
    public static async Task<bool> IsCurrentAsync(
        ClaimsPrincipal? principal,
        ILocalIdentityStore identities,
        IAuthorityLifecycleStore lifecycle,
        CancellationToken cancellationToken = default)
    {
        var subject = principal?.FindFirst("sub")?.Value;
        var versionText = principal?.FindFirst(JwtTokenIssuer.LocalUserVersionClaim)?.Value;
        var epochText = principal?.FindFirst(JwtTokenIssuer.AuthorityEpochClaim)?.Value;
        if (!Guid.TryParse(subject, out var userId) ||
            !long.TryParse(versionText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var tokenVersion) ||
            !long.TryParse(epochText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var tokenEpoch))
        {
            return false;
        }

        try
        {
            var account = await identities.FindByIdAsync(userId, cancellationToken);
            var state = await lifecycle.GetAsync(cancellationToken);
            return account is not null &&
                account.IsEnabled &&
                account.UpdatedAtUtc.ToUnixTimeMilliseconds() == tokenVersion &&
                AuthorityLifecycleSessionFence.IsCurrent(state, tokenEpoch);
        }
        catch
        {
            return false;
        }
    }
}
