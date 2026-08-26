using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public sealed class JwtTokenIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _credentials;
    private readonly TimeSpan _lifetime;

    public JwtTokenIssuer(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Authentication:Jwt");
        _issuer = jwt["Issuer"]?.Trim()
            ?? throw new InvalidOperationException("Authentication:Jwt:Issuer is required for token issuance.");
        _audience = jwt["Audience"]?.Trim()
            ?? throw new InvalidOperationException("Authentication:Jwt:Audience is required for token issuance.");
        var signingKey = jwt["SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes for token issuance.");

        var minutes = configuration.GetValue<int?>("Authentication:Local:AccessTokenMinutes") ?? 480;
        if (minutes is < 5 or > 1440)
            throw new InvalidOperationException("Authentication:Local:AccessTokenMinutes must be between 5 and 1440.");

        _lifetime = TimeSpan.FromMinutes(minutes);
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public IssuedAccessToken Issue(LocalUserAccount account, DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!account.IsEnabled)
            throw new InvalidOperationException("Disabled users cannot receive access tokens.");

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var expires = now.Add(_lifetime);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.Username),
            new("name", account.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
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
}
