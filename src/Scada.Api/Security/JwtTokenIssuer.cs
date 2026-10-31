using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public sealed class JwtTokenIssuer
{
    public const string IdentityProviderClaim = "elitescada_identity_provider";
    public const string LocalIdentityProvider = "local";
    public const string PublicRuntimeIdentityProvider = "public-runtime";
    public const string LocalUserVersionClaim = "elitescada_local_user_version";
    public const string AuthorityEpochClaim = "elitescada_authority_epoch";

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _credentials;
    private readonly int _configuredDefaultLifetimeMinutes;
    private readonly IAuthorityLifecycleStore _authorityLifecycle;
    private readonly ILocalAuthenticationSessionPolicyStore? _localSessionPolicy;

    public JwtTokenIssuer(
        IConfiguration configuration,
        IAuthorityLifecycleStore authorityLifecycle,
        ILocalAuthenticationSessionPolicyStore? localSessionPolicy = null)
    {
        _authorityLifecycle = authorityLifecycle ?? throw new ArgumentNullException(nameof(authorityLifecycle));
        _localSessionPolicy = localSessionPolicy;
        var jwt = configuration.GetSection("Authentication:Jwt");
        _issuer = jwt["Issuer"]?.Trim()
            ?? throw new InvalidOperationException("Authentication:Jwt:Issuer is required for token issuance.");
        _audience = jwt["Audience"]?.Trim()
            ?? throw new InvalidOperationException("Authentication:Jwt:Audience is required for token issuance.");
        var signingKey = jwt["SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes for token issuance.");

        var minutes = configuration.GetValue<int?>("Authentication:Local:AccessTokenMinutes")
            ?? LocalAuthenticationSessionPolicy.DefaultAccessTokenMinutes;
        try
        {
            _configuredDefaultLifetimeMinutes = LocalAuthenticationSessionPolicy.ValidateAccessTokenMinutes(minutes);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidOperationException(
                "Authentication:Local:AccessTokenMinutes must be between 5 and 1440.",
                ex);
        }
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public async Task<IssuedAccessToken> IssueAsync(
        LocalUserAccount account,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!account.IsEnabled)
            throw new InvalidOperationException("Disabled users cannot receive access tokens.");

        var lifecycle = await _authorityLifecycle.GetAsync(cancellationToken);
        if (!lifecycle.AllowsAuthorization)
            throw new InvalidOperationException("Local Authority is not available for token issuance.");

        var lifetimeMinutes = _localSessionPolicy is null
            ? _configuredDefaultLifetimeMinutes
            : (await _localSessionPolicy.GetAsync(cancellationToken)).AccessTokenMinutes;
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(lifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.Username),
            new("name", account.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(IdentityProviderClaim, LocalIdentityProvider),
            new(LocalUserVersionClaim, account.UpdatedAtUtc.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(AuthorityEpochClaim, lifecycle.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        claims.AddRange(LocalIdentityNormalization.NormalizeRoles(account.Roles)
            .Select(role => new Claim("role", role)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public async Task<IssuedAccessToken> IssuePublicRuntimeSessionAsync(
        string subjectId,
        Guid sessionId,
        string clientInstanceId,
        long policyVersion,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientInstanceId);
        if (sessionId == Guid.Empty || policyVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(sessionId));

        var lifecycle = await _authorityLifecycle.GetAsync(cancellationToken);
        if (!lifecycle.AllowsAuthorization)
            throw new InvalidOperationException("Local Authority is not available for public Runtime session issuance.");

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(15);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subjectId),
            new Claim("name", "Public Runtime visitor"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim(IdentityProviderClaim, PublicRuntimeIdentityProvider),
            new Claim(PublicRuntimeSessionSecurity.MarkerClaim, "1"),
            new Claim(PublicRuntimeSessionSecurity.SessionIdClaim, sessionId.ToString("D")),
            new Claim(PublicRuntimeSessionSecurity.ClientInstanceClaim, clientInstanceId),
            new Claim(PublicRuntimeSessionSecurity.PolicyVersionClaim, policyVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim(AuthorityEpochClaim, lifecycle.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);
        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
