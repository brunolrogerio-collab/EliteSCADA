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

public sealed class SecurityScopeHierarchyTests
{
    [Fact]
    public void StableScopeIdentityPreservesRenameAndControlsDescendantsWithoutSiblingLeakage()
    {
        var equipmentId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var tagId = Guid.Parse("71000000-0000-0000-0000-000000000002");
        var siblingTagId = Guid.Parse("71000000-0000-0000-0000-000000000003");
        var areaId = Guid.Parse("71000000-0000-0000-0000-000000000010");
        var equipmentNodeId = Guid.Parse("71000000-0000-0000-0000-000000000011");
        var tagNodeId = Guid.Parse("71000000-0000-0000-0000-000000000012");
        var siblingAreaId = Guid.Parse("71000000-0000-0000-0000-000000000020");
        var siblingTagNodeId = Guid.Parse("71000000-0000-0000-0000-000000000021");
        var graph = Graph(
            new(areaId, "utilities", "Utilities", SecurityScopeNodeKind.Area),
            new(equipmentNodeId, "pump-101", "Pump P-101", SecurityScopeNodeKind.Equipment, areaId, equipmentId),
            new(tagNodeId, "pump-101-speed", "Pump speed", SecurityScopeNodeKind.Tag, equipmentNodeId, tagId),
            new(siblingAreaId, "treatment", "Treatment", SecurityScopeNodeKind.Area),
            new(siblingTagNodeId, "tank-level", "Tank level", SecurityScopeNodeKind.Tag, siblingAreaId, siblingTagId));
        var authorization = Policies(new CapabilityGrant(
            SecurityCapability.ProcessValueWrite,
            new AuthorizationScope(ScopeNodeId: areaId, IncludeDescendants: true)));

        var allowedBeforeRename = authorization.Evaluate(
            Principal(),
            SecurityCapability.ProcessValueWrite,
            graph.Enrich(new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Tag, ResourceId: tagId)));
        Assert.True(allowedBeforeRename.Allowed);

        var renamed = Graph(
            new(areaId, "utilities", "Utilities renamed", SecurityScopeNodeKind.Area),
            new(equipmentNodeId, "pump-101", "Pump P-101 renamed", SecurityScopeNodeKind.Equipment, areaId, equipmentId),
            new(tagNodeId, "pump-101-speed", "Pump speed renamed", SecurityScopeNodeKind.Tag, equipmentNodeId, tagId),
            new(siblingAreaId, "treatment", "Treatment", SecurityScopeNodeKind.Area),
            new(siblingTagNodeId, "tank-level", "Tank level", SecurityScopeNodeKind.Tag, siblingAreaId, siblingTagId));
        Assert.True(authorization.Evaluate(
            Principal(),
            SecurityCapability.ProcessValueWrite,
            renamed.Enrich(new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Tag, ResourceId: tagId))).Allowed);
        Assert.False(authorization.Evaluate(
            Principal(),
            SecurityCapability.ProcessValueWrite,
            graph.Enrich(new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Tag, ResourceId: siblingTagId))).Allowed);

        var noDescendants = Policies(new CapabilityGrant(
            SecurityCapability.ProcessValueWrite,
            new AuthorizationScope(ScopeNodeId: areaId, IncludeDescendants: false)));
        Assert.False(noDescendants.Evaluate(
            Principal(),
            SecurityCapability.ProcessValueWrite,
            graph.Enrich(new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Tag, ResourceId: tagId))).Allowed);
    }

    [Fact]
    public void ScopeBindingsCoverTagCommandEquipmentAndScreenAndRejectStaleResourceReuse()
    {
        var equipmentId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        var tagId = Guid.Parse("72000000-0000-0000-0000-000000000002");
        var commandId = Guid.Parse("72000000-0000-0000-0000-000000000003");
        var screenId = Guid.Parse("72000000-0000-0000-0000-000000000004");
        var equipmentNodeId = Guid.Parse("72000000-0000-0000-0000-000000000010");
        var graph = Graph(
            new(equipmentNodeId, "pump", "Pump", SecurityScopeNodeKind.Equipment, ResourceId: equipmentId),
            new(Guid.Parse("72000000-0000-0000-0000-000000000011"), "pump-tag", "Pump TAG", SecurityScopeNodeKind.Tag, equipmentNodeId, tagId),
            new(Guid.Parse("72000000-0000-0000-0000-000000000012"), "pump-command", "Pump command", SecurityScopeNodeKind.Command, equipmentNodeId, commandId),
            new(Guid.Parse("72000000-0000-0000-0000-000000000013"), "pump-screen", "Pump screen", SecurityScopeNodeKind.Screen, equipmentNodeId, screenId));
        var authorization = Policies(new CapabilityGrant(
            SecurityCapability.CommandExecute,
            new AuthorizationScope(ScopeNodeId: equipmentNodeId, IncludeDescendants: true)));

        foreach (var resource in new[]
                 {
                     new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Tag, ResourceId: tagId),
                     new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Command, ResourceId: commandId),
                     new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Screen, ResourceId: screenId)
                 })
        {
            Assert.True(authorization.Evaluate(
                Principal(),
                SecurityCapability.CommandExecute,
                graph.Enrich(resource)).Allowed);
        }

        Assert.False(authorization.Evaluate(
            Principal(),
            SecurityCapability.CommandExecute,
            graph.Enrich(new AuthorizationResource(
                ResourceKind: AuthorizationResourceKind.Tag,
                ResourceId: Guid.Parse("72000000-0000-0000-0000-000000000099")))).Allowed);

        Assert.False(authorization.Evaluate(
            Principal(),
            SecurityCapability.CommandExecute,
            graph.Enrich(new AuthorizationResource(
                ResourceKind: AuthorizationResourceKind.Tag,
                ResourceId: Guid.Parse("72000000-0000-0000-0000-000000000099"),
                ScopeNodeId: equipmentNodeId,
                ScopeNodeAncestry: new[] { equipmentNodeId }))).Allowed);
    }

    [Fact]
    public void ScopeGraphRejectsDuplicateCycleAndMissingParent()
    {
        var first = Guid.Parse("73000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("73000000-0000-0000-0000-000000000002");

        var errors = SecurityScopeGraph.Validate(new[]
        {
            new SecurityScopeEngineeringDto(first, "duplicate", "First", SecurityScopeNodeKind.Area, second),
            new SecurityScopeEngineeringDto(second, "duplicate", "Second", SecurityScopeNodeKind.Area, first),
            new SecurityScopeEngineeringDto(Guid.Parse("73000000-0000-0000-0000-000000000003"), "orphan", "Orphan", SecurityScopeNodeKind.Area, Guid.NewGuid())
        });

        Assert.Contains(errors, error => error.Code == "SECURITY_SCOPE_KEY_DUPLICATE");
        Assert.Contains(errors, error => error.Code == "SECURITY_SCOPE_CYCLE");
        Assert.Contains(errors, error => error.Code == "SECURITY_SCOPE_PARENT_NOT_FOUND");
    }

    [Fact]
    public void PreviewBlocksScopeBoundToMissingStableResource()
    {
        var service = CreateService();
        var package = service.ExportPackage() with
        {
            SecurityScopes = new[]
            {
                new SecurityScopeEngineeringDto(
                    Guid.Parse("74000000-0000-0000-0000-000000000001"),
                    "missing-tag",
                    "Missing TAG",
                    SecurityScopeNodeKind.Tag,
                    ResourceId: Guid.Parse("74000000-0000-0000-0000-000000000002"))
            }
        };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.Code == "SECURITY_SCOPE_RESOURCE_NOT_FOUND");
    }

    [Fact]
    public void PartialRoleImportResolvesAStableScopeAlreadyInTheWorkspace()
    {
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var scopeId = Guid.Parse("74500000-0000-0000-0000-000000000001");
        security.UpsertScope(new SecurityScopeEngineeringDto(
            scopeId,
            "plant-a",
            "Plant A",
            SecurityScopeNodeKind.Plant));
        var service = CreateService(security);
        var package = service.ExportPackage() with
        {
            SecurityRoles = new[]
            {
                new SecurityRoleEngineeringDto(
                    Guid.NewGuid(),
                    "operator",
                    "Operator",
                    Grants: new[]
                    {
                        new CapabilityGrantEngineeringDto(
                            SecurityCapability.EngineeringView,
                            new AuthorizationScopeEngineeringDto(ScopeNodeId: scopeId))
                    })
            },
            SecurityScopes = Array.Empty<SecurityScopeEngineeringDto>()
        };

        Assert.True(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
    }

    [Fact]
    public void SchemaV17ExactLegacyTagPathMigratesToStableScopeNode()
    {
        var service = CreateService();
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 17,
          "exportedAt": "2026-09-12T00:00:00Z",
          "tags": [{
            "id": "75000000-0000-0000-0000-000000000001",
            "name": "Setpoint",
            "path": "Plant.P01.Setpoint",
            "dataType": "double"
          }],
          "alarms": [],
          "securityRoles": [{
            "key": "operator",
            "name": "Operator",
            "grants": [{
              "capability": "processValueWrite",
              "scope": { "tagPath": "Plant.P01.Setpoint" }
            }]
          }]
        }
        """;

        var package = service.ParseJson(json);
        var scope = Assert.Single(Assert.Single(package.SecurityRoles!).Grants!).Scope;
        var node = Assert.Single(package.SecurityScopes!);

        Assert.Equal(SecurityScopeNodeKind.Tag, node.Kind);
        Assert.Equal(Guid.Parse("75000000-0000-0000-0000-000000000001"), node.ResourceId);
        Assert.Equal(node.Id, scope!.ScopeNodeId);
        Assert.Null(scope.TagPath);
        Assert.True(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
    }

    [Fact]
    public void WildcardOrAmbiguousLegacyScopeFailsClosedDuringPreview()
    {
        var service = CreateService();
        var package = service.ExportPackage() with
        {
            SchemaVersion = 17,
            Tags = new[]
            {
                new TagEngineeringDto(Guid.NewGuid(), "One", "Plant.P01.Setpoint", TagDataType.Double),
                new TagEngineeringDto(Guid.NewGuid(), "Two", "Plant.P01.Setpoint", TagDataType.Double)
            },
            SecurityRoles = new[]
            {
                new SecurityRoleEngineeringDto(
                    Guid.NewGuid(),
                    "operator",
                    "Operator",
                    Grants: new[]
                    {
                        new CapabilityGrantEngineeringDto(
                            SecurityCapability.ProcessValueWrite,
                            new AuthorizationScopeEngineeringDto(TagPath: "Plant.P01.*"))
                    })
            }
        };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.Code == "SECURITY_LEGACY_SCOPE_MIGRATION_BLOCKED");
    }

    [Fact]
    public void AmbiguousExactLegacyScopeFailsClosedInsteadOfPickingAResource()
    {
        var service = CreateService();
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 17,
          "exportedAt": "2026-09-12T00:00:00Z",
          "tags": [
            { "id": "76000000-0000-0000-0000-000000000001", "name": "One", "path": "Plant.P01.Setpoint", "dataType": "double" },
            { "id": "76000000-0000-0000-0000-000000000002", "name": "Two", "path": "Plant.P01.Setpoint", "dataType": "double" }
          ],
          "alarms": [],
          "securityRoles": [{
            "key": "operator", "name": "Operator",
            "grants": [{ "capability": "processValueWrite", "scope": { "tagPath": "Plant.P01.Setpoint" } }]
          }]
        }
        """;

        var package = service.ParseJson(json);
        var scope = Assert.Single(Assert.Single(package.SecurityRoles!).Grants!).Scope;

        Assert.Null(scope!.ScopeNodeId);
        Assert.False(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        Assert.Contains(service.Preview(package, ImportMode.CreateAndUpdate).Items.SelectMany(item => item.Issues), issue =>
            issue.Code == "SECURITY_LEGACY_SCOPE_MIGRATION_BLOCKED");
    }

    private static SecurityScopeGraph Graph(params SecurityScopeEngineeringDto[] scopes)
    {
        Assert.True(SecurityScopeGraph.TryCreate(scopes, out var graph, out var errors));
        Assert.Empty(errors);
        return graph!;
    }

    private static InMemoryCapabilityAuthorizationService Policies(params CapabilityGrant[] grants) =>
        new(new[] { new RolePolicy("scope-role", "Scope role", grants) });

    private static SecurityPrincipal Principal() => new("scope-user", null, new[] { "scope-role" });

    private static EngineeringExchangeService CreateService(
        InMemorySecurityPolicyEngineeringRegistry? security = null)
    {
        var bus = new InMemoryScadaEventBus();
        return new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            new InMemoryAlarmEngine(bus),
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            security ?? new InMemorySecurityPolicyEngineeringRegistry());
    }
}
