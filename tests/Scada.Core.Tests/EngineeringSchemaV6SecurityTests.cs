using System.Text.Json;
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
        var json = service.ExportJson();
        var parsed = service.ParseJson(json);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Contains("\"capability\": \"processValueWrite\"", json, StringComparison.Ordinal);
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

    [Fact]
    public void SchemaV16MigrationSynthesizesEngineeringViewFromLegacyModifyGrant()
    {
        var service = CreateService(new InMemorySecurityPolicyEngineeringRegistry());
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 16,
          "exportedAt": "2026-09-12T00:00:00Z",
          "tags": [],
          "alarms": [],
          "securityRoles": [
            {
              "id": "60000000-0000-0000-0000-000000000003",
              "key": "legacy-engineer",
              "name": "Legacy Engineer",
              "grants": [
                {
                  "capability": "engineeringModify",
                  "scope": { "tagPath": "Plant.P01.*" }
                }
              ]
            }
          ]
        }
        """;

        var package = service.ParseJson(json);
        var role = Assert.Single(package.SecurityRoles!);

        Assert.Contains(role.Grants!, grant =>
            grant.Capability == SecurityCapability.EngineeringModify &&
            grant.Scope?.TagPath == "Plant.P01.*");
        Assert.Contains(role.Grants!, grant =>
            grant.Capability == SecurityCapability.EngineeringView &&
            grant.Scope?.TagPath == "Plant.P01.*");
    }

    [Fact]
    public void SchemaV17KeepsEngineeringViewIndependentFromEngineeringModify()
    {
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        security.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.Parse("60000000-0000-0000-0000-000000000004"),
            "modifier",
            "Modifier",
            Grants: new[] { new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify) }));
        var service = CreateService(security);

        var package = service.ParseJson(service.ExportJson());
        var role = Assert.Single(package.SecurityRoles!);

        Assert.Contains(role.Grants!, grant => grant.Capability == SecurityCapability.EngineeringModify);
        Assert.DoesNotContain(role.Grants!, grant => grant.Capability == SecurityCapability.EngineeringView);
    }

    [Theory]
    [InlineData("\"futureUnknownCapability\"")]
    [InlineData("999")]
    public void ParserRejectsUnknownOrOrdinalCapabilityInput(string capabilityJson)
    {
        var service = CreateService(new InMemorySecurityPolicyEngineeringRegistry());
        var json = $$"""
        {
          "schema": "scada.engineering",
          "schemaVersion": 17,
          "exportedAt": "2026-09-12T00:00:00Z",
          "tags": [],
          "alarms": [],
          "securityRoles": [
            {
              "key": "invalid",
              "name": "Invalid",
              "grants": [{ "capability": {{capabilityJson}} }]
            }
          ]
        }
        """;

        Assert.Throws<JsonException>(() => service.ParseJson(json));
    }

    [Fact]
    public void PreviewRejectsUnknownAndAmbiguousTagAccessRoleReferences()
    {
        var service = CreateService(new InMemorySecurityPolicyEngineeringRegistry());
        var duplicateRole = new SecurityRoleEngineeringDto(null, "operator", "Operator");
        var package = service.ExportPackage() with
        {
            Tags = new[]
            {
                new TagEngineeringDto(
                    Guid.Parse("60000000-0000-0000-0000-000000000005"),
                    "Setpoint",
                    "Plant.P01.Setpoint",
                    TagDataType.Double,
                    ReadOnly: false,
                    AccessPolicy: new TagAccessPolicyDto(
                        ReadRoles: new[] { "missing" },
                        WriteRoles: new[] { "operator" }))
            },
            SecurityRoles = new[] { duplicateRole, duplicateRole with { Id = Guid.NewGuid() } }
        };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "TAG_ACCESS_ROLE_NOT_FOUND");
        Assert.Contains(issues, issue => issue.Code == "TAG_ACCESS_ROLE_AMBIGUOUS");
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
