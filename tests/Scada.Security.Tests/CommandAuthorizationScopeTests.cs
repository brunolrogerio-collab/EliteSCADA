using Scada.Security.Authorization;

namespace Scada.Security.Tests;

public sealed class CommandAuthorizationScopeTests
{
    [Fact]
    public void CommandGrant_CanConstrainAreaEquipmentTagAndCommandKeyTogether()
    {
        var authorization = new InMemoryCapabilityAuthorizationService(new[]
        {
            new RolePolicy(
                "p01-operator",
                "P01 Operator",
                new[]
                {
                    new CapabilityGrant(
                        SecurityCapability.CommandExecute,
                        new AuthorizationScope(
                            Area: "Plant",
                            EquipmentPath: "Plant.P01",
                            TagPath: "Plant.P01.CommandWord",
                            CommandKey: "plant.p01.*"))
                })
        });
        var principal = new SecurityPrincipal("operator-1", null, new[] { "p01-operator" });

        Assert.True(authorization.Evaluate(
            principal,
            SecurityCapability.CommandExecute,
            new AuthorizationResource(
                Area: "Plant",
                EquipmentPath: "Plant.P01",
                TagPath: "Plant.P01.CommandWord",
                CommandKey: "plant.p01.start")).Allowed);

        Assert.False(authorization.Evaluate(
            principal,
            SecurityCapability.CommandExecute,
            new AuthorizationResource(
                Area: "Plant",
                EquipmentPath: "Plant.P02",
                TagPath: "Plant.P02.CommandWord",
                CommandKey: "plant.p02.start")).Allowed);
    }
}
