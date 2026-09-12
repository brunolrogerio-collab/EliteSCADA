using Scada.Core.Tags;
using Scada.Security.Authorization;

namespace Scada.Security.Tests;

public sealed class CapabilityAuthorizationTests
{
    [Theory]
    [InlineData("administrator")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public void RoleNamesAreConfigurableAndDoNotImplyCapabilities(string unprivilegedRole)
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "night-shift",
                "Night Shift",
                new[] { new CapabilityGrant(SecurityCapability.CommandExecute) }),
            new RolePolicy(
                unprivilegedRole,
                unprivilegedRole,
                Array.Empty<CapabilityGrant>())
        });

        var nightShift = new SecurityPrincipal("u1", "User 1", new[] { "night-shift" });
        var unprivileged = new SecurityPrincipal("u2", "User 2", new[] { unprivilegedRole });

        Assert.True(authorization.Evaluate(nightShift, SecurityCapability.CommandExecute).Allowed);
        Assert.False(authorization.Evaluate(unprivileged, SecurityCapability.CommandExecute).Allowed);
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
                new[] { new CapabilityGrant(SecurityCapability.ProcessValueWrite) }),
            new RolePolicy(
                "supervisor",
                "Supervisor",
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

    [Fact]
    public void ExplicitTagRoleMatchCannotSubstituteForRequiredCapability()
    {
        var capabilities = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy("supervisor", "Supervisor", Array.Empty<CapabilityGrant>())
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

        var decision = access.Evaluate(
            new SecurityPrincipal("u1", null, new[] { "supervisor" }),
            tag,
            TagAccessOperation.Write);

        Assert.False(decision.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, decision.Capability);
    }

    [Fact]
    public void TagRestrictionMustMatchTheRoleThatGrantedTheCapability()
    {
        var capabilities = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "operator",
                "Operator",
                new[] { new CapabilityGrant(SecurityCapability.ProcessValueWrite) }),
            new RolePolicy("supervisor", "Supervisor", Array.Empty<CapabilityGrant>())
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

        var decision = access.Evaluate(
            new SecurityPrincipal("u1", null, new[] { "operator", "supervisor" }),
            tag,
            TagAccessOperation.Write);

        Assert.False(decision.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, decision.Capability);
    }

    [Fact]
    public void HighAvailabilityCapabilitiesAreIndependentAndDenyByDefault()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "ha-observer",
                "HA Observer",
                new[] { new CapabilityGrant(SecurityCapability.HighAvailabilityObserve) })
        });
        var principal = new SecurityPrincipal("u1", null, new[] { "ha-observer" });

        Assert.True(authorization.Evaluate(principal, SecurityCapability.HighAvailabilityObserve).Allowed);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.HighAvailabilityTransfer).Allowed);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.HighAvailabilityAdmin).Allowed);
    }

    [Fact]
    public void MultipleRolesComposeAdditivelyWithoutCrossCapabilityImplication()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy("reader", "Reader", new[] { new CapabilityGrant(SecurityCapability.EngineeringView) }),
            new RolePolicy("writer", "Writer", new[] { new CapabilityGrant(SecurityCapability.ProcessValueWrite) })
        });
        var principal = new SecurityPrincipal("u1", null, new[] { "writer", "reader" });

        Assert.Equal(new[] { "reader" }, authorization
            .Evaluate(principal, SecurityCapability.EngineeringView).MatchedRoles);
        Assert.Equal(new[] { "writer" }, authorization
            .Evaluate(principal, SecurityCapability.ProcessValueWrite).MatchedRoles);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.CommandExecute).Allowed);
    }
}
