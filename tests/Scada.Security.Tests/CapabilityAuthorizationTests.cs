using Scada.Core.Tags;
using Scada.Security.Authorization;

namespace Scada.Security.Tests;

public sealed class CapabilityAuthorizationTests
{
    [Fact]
    public void RoleNamesAreConfigurableAndDoNotImplyCapabilities()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "night-shift",
                "Night Shift",
                new[] { new CapabilityGrant(SecurityCapability.CommandExecute) }),
            new RolePolicy(
                "administrator",
                "Administrator",
                Array.Empty<CapabilityGrant>())
        });

        var nightShift = new SecurityPrincipal("u1", "User 1", new[] { "night-shift" });
        var administrator = new SecurityPrincipal("u2", "User 2", new[] { "administrator" });

        Assert.True(authorization.Evaluate(nightShift, SecurityCapability.CommandExecute).Allowed);
        Assert.False(authorization.Evaluate(administrator, SecurityCapability.CommandExecute).Allowed);
    }

    [Fact]
    public void ScopedGrantOnlyMatchesConfiguredResourcePrefix()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "area-one-operator",
                "Area One Operator",
                new[]
                {
                    new CapabilityGrant(
                        SecurityCapability.ProcessValueWrite,
                        new AuthorizationScope(TagPath: "Plant.Area1.*"))
                })
        });
        var principal = new SecurityPrincipal("u1", null, new[] { "area-one-operator" });

        Assert.True(authorization.Evaluate(
            principal,
            SecurityCapability.ProcessValueWrite,
            new AuthorizationResource(TagPath: "Plant.Area1.P01.Speed")).Allowed);
        Assert.False(authorization.Evaluate(
            principal,
            SecurityCapability.ProcessValueWrite,
            new AuthorizationResource(TagPath: "Plant.Area2.P01.Speed")).Allowed);
    }

    [Fact]
    public void UnauthenticatedPrincipalIsDeniedEvenWhenRoleWouldGrantCapability()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "operator",
                "Operator",
                new[] { new CapabilityGrant(SecurityCapability.TagRead) })
        });
        var principal = new SecurityPrincipal("anonymous", null, new[] { "operator" }, IsAuthenticated: false);

        var result = authorization.Evaluate(principal, SecurityCapability.TagRead);

        Assert.False(result.Allowed);
        Assert.Contains("not authenticated", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TagAccessPolicyPreservesNullVersusEmptyRoleSemantics()
    {
        var capabilities = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "operator",
                "Operator",
                new[] { new CapabilityGrant(SecurityCapability.ProcessValueWrite) })
        });
        var access = new TagAccessAuthorization(capabilities);
        var principal = new SecurityPrincipal("u1", null, new[] { "operator" });

        var inherited = new TagDefinition(
            Guid.NewGuid(),
            "Frequency",
            "Plant.P01.Frequency",
            TagDataType.Double,
            null,
            "Hz",
            null,
            false,
            AccessPolicy: new TagAccessPolicy(WriteRoles: null));

        var explicitlyDenied = inherited with
        {
            Id = Guid.NewGuid(),
            Path = "Plant.P02.Frequency",
            AccessPolicy = new TagAccessPolicy(WriteRoles: Array.Empty<string>())
        };

        Assert.True(access.Evaluate(principal, inherited, TagAccessOperation.Write).Allowed);
        Assert.False(access.Evaluate(principal, explicitlyDenied, TagAccessOperation.Write).Allowed);
    }

    [Fact]
    public void ExplicitTagRoleListOverridesGeneralCapability()
    {
        var capabilities = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "operator",
                "Operator",
                new[] { new CapabilityGrant(SecurityCapability.ProcessValueWrite) })
        });
        var access = new TagAccessAuthorization(capabilities);
        var tag = new TagDefinition(
            Guid.NewGuid(),
            "Setpoint",
            "Plant.P01.Setpoint",
            TagDataType.Double,
            null,
            null,
            null,
            false,
            AccessPolicy: new TagAccessPolicy(WriteRoles: new[] { "supervisor" }));

        Assert.False(access.Evaluate(
            new SecurityPrincipal("u1", null, new[] { "operator" }),
            tag,
            TagAccessOperation.Write).Allowed);
        Assert.True(access.Evaluate(
            new SecurityPrincipal("u2", null, new[] { "supervisor" }),
            tag,
            TagAccessOperation.Write).Allowed);
    }
}
