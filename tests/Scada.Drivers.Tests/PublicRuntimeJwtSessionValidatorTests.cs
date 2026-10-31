using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Scada.Api.Security;
using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class PublicRuntimeJwtSessionValidatorTests
{
    [Fact]
    public async Task Public_runtime_token_is_revoked_when_authority_enters_detach_and_after_reattach()
    {
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        await lifecycle.MarkAuthorityPresentAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "test-issuer",
                ["Authentication:Jwt:Audience"] = "test-audience",
                ["Authentication:Jwt:SigningKey"] = "01234567890123456789012345678901"
            })
            .Build();
        var issuer = new JwtTokenIssuer(configuration, lifecycle);
        var issued = await issuer.IssuePublicRuntimeSessionAsync(
            "public-runtime:test",
            Guid.NewGuid(),
            "public-client-test",
            policyVersion: 4);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "test"));

        Assert.True(PublicRuntimeSessionSecurity.TryRead(principal, out var identity));
        Assert.Equal(4, identity.PolicyVersion);
        Assert.True(await PublicRuntimeJwtSessionValidator.IsCurrentAsync(principal, lifecycle));

        await lifecycle.BeginDetachAsync();
        Assert.False(await PublicRuntimeJwtSessionValidator.IsCurrentAsync(principal, lifecycle));

        await lifecycle.CompleteDetachAsync();
        await lifecycle.BeginAttachAsync();
        await lifecycle.CompleteAttachAsync();
        Assert.False(await PublicRuntimeJwtSessionValidator.IsCurrentAsync(principal, lifecycle));
    }

    [Fact]
    public void Public_runtime_token_without_authority_epoch_is_rejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "public-runtime:test"),
            new Claim(PublicRuntimeSessionSecurity.MarkerClaim, "1"),
            new Claim(PublicRuntimeSessionSecurity.SessionIdClaim, Guid.NewGuid().ToString("D")),
            new Claim(PublicRuntimeSessionSecurity.ClientInstanceClaim, "public-client-test"),
            new Claim(PublicRuntimeSessionSecurity.PolicyVersionClaim, "4")
        ],
        "test"));

        Assert.False(PublicRuntimeSessionSecurity.TryRead(principal, out _));
    }
}
