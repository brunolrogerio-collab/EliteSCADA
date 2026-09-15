using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Scada.Api.Security;
using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class LocalAuthorityJwtSessionValidatorTests
{
    [Fact]
    public async Task LocalJwt_IsAcceptedOnlyForAttachedCurrentAuthorityEpoch()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        await lifecycle.MarkAuthorityPresentAsync();
        var now = DateTimeOffset.UtcNow;
        var account = new LocalUserAccount(
            Guid.NewGuid(),
            "epoch-admin",
            LocalIdentityNormalization.NormalizeUsername("epoch-admin"),
            "Epoch Admin",
            true,
            ["developer"],
            LocalPasswordHasher.Hash("epoch-validation-password"),
            now,
            now);
        await identities.CreateAsync(account);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "test-issuer",
                ["Authentication:Jwt:Audience"] = "test-audience",
                ["Authentication:Jwt:SigningKey"] = "01234567890123456789012345678901",
                ["Authentication:Local:AccessTokenMinutes"] = "60"
            })
            .Build();
        var issuer = new JwtTokenIssuer(configuration, lifecycle);
        var issued = await issuer.IssueAsync(account);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "test"));

        Assert.True(await LocalAuthorityJwtSessionValidator.IsCurrentAsync(principal, identities, lifecycle));

        await lifecycle.BeginDetachAsync();
        Assert.False(await LocalAuthorityJwtSessionValidator.IsCurrentAsync(principal, identities, lifecycle));

        await lifecycle.CompleteDetachAsync();
        Assert.False(await LocalAuthorityJwtSessionValidator.IsCurrentAsync(principal, identities, lifecycle));
    }
}
