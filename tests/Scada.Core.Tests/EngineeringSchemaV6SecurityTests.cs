using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Security.Authorization;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV6SecurityTests
{
    [Fact]
    public void SchemaV6_RoundTripsSecurityRolesAndCompilesRuntimePolicy()
    {
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        security.UpsertRole(new SecurityRoleEngineeringDto(
            Id: Guid.Parse("60000000-0000-0000-0000-000000000001"),
            Key: "area-one-operator",
            Name: "Area One Operator",
            Grants: new[]
            {
                new CapabilityGrantEngineeringDto(
                    SecurityCapability.ProcessValueWrite,
                    new AuthorizationScopeEngineeringDto(TagPath: "Plant.Area1.*")),
                new CapabilityGrantEngineeringDto(SecurityCapability.AlarmAcknowledge)
            }));

        var service = CreateService(security);
        var parsed = service.ParseJson(service.ExportJson());

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, parsed.SchemaVersion);
        var role = Assert.Single(parsed.SecurityRoles!);
        Assert.Equal("area-one-operator", role.Key);
        Assert.Contains(role.Grants!, x =>
            x.Capability == SecurityCapability.ProcessValueWrite &&
            x.Scope?.TagPath == "Plant.Area1.*");

        var authorization = new InMemoryCapabilityAuthorizationService(
            SecurityPolicyCompiler.Compile(parsed.SecurityRoles!));
        var principal = new SecurityPrincipal("operator-1", null, new[] { "area-one-operator" });

        Assert.True(authorization.Evaluate(
            principal,
            SecurityCapability.ProcessValueWrite,
            new AuthorizationResource(TagPath: "Plant.Area1.P01.Setpoint")).Allowed);
        Assert.False(authorization.Evaluate(
            principal,
            SecurityCapability.ProcessValueWrite,
            new AuthorizationResource(TagPath: "Plant.Area2.P01.Setpoint")).Allowed);
    }

    [Fact]
    public void PreviewRejectsDuplicateRoleAndSecretLikeSecurityMetadata()
    {
        var service = CreateService(new InMemorySecurityPolicyEngineeringRegistry());
        var role = new SecurityRoleEngineeringDto(
            null,
            "operator",
            "Operator",
            Grants: new[] { new CapabilityGrantEngineeringDto(SecurityCapability.TagRead) },
            Metadata: new Dictionary<string, string> { ["passwordHash"] = "must-not-be-here" });
        var package = service.ExportPackage() with
        {
            SecurityRoles = new[] { role, role with { Id = Guid.NewGuid() } }
        };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "SECURITY_ROLE_DUPLICATE_IN_FILE");
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "SECURITY_SECRET_METADATA_FORBIDDEN");
    }

    [Fact]
    public void SchemaV5LoadsWithEmptySecurityRolesForBackwardCompatibility()
    {
        var service = CreateService(new InMemorySecurityPolicyEngineeringRegistry());
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 5,
          "exportedAt": "2026-05-01T00:00:00Z",
          "tags": [],
          "alarms": [],
          "dataSources": [],
          "templates": [],
          "equipment": [],
          "dynamos": [],
          "screens": [],
          "popups": []
        }
        """;

        var package = service.ParseJson(json);

        Assert.NotNull(package.SecurityRoles);
        Assert.Empty(package.SecurityRoles!);
    }

    [Fact]
    public void ApplySecurityRolePreservesStableIdWhenKeyMatches()
    {
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var existingId = Guid.Parse("60000000-0000-0000-0000-000000000002");
        security.UpsertRole(new SecurityRoleEngineeringDto(existingId, "operator", "Old Operator"));
        var service = CreateService(security);
        var package = service.ExportPackage() with
        {
            SecurityRoles = new[]
            {
                new SecurityRoleEngineeringDto(
                    null,
                    "operator",
                    "Updated Operator",
                    Grants: new[] { new CapabilityGrantEngineeringDto(SecurityCapability.CommandExecute) })
            }
        };

        var result = service.Apply(package, ImportMode.CreateAndUpdate);
        var updated = security.FindRoleByKey("operator");

        Assert.Empty(result.Issues);
        Assert.NotNull(updated);
        Assert.Equal(existingId, updated!.Id);
        Assert.Equal("Updated Operator", updated.Name);
        Assert.Contains(updated.Grants!, x => x.Capability == SecurityCapability.CommandExecute);
    }

    private static EngineeringExchangeService CreateService(ISecurityPolicyEngineeringRegistry security)
    {
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            security);
    }
}
