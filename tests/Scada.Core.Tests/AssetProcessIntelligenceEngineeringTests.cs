using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Security;
using Scada.Engineering.DataSources;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authorization;

namespace Scada.Core.Tests;

public sealed class AssetProcessIntelligenceEngineeringTests
{
    [Fact]
    public void Schema24ExportsAndRestoresStableEligibleScopesAndActivationState()
    {
        var locationId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var equipmentId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
        var registry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        registry.Replace(new AssetProcessIntelligenceEngineeringDto(Scopes:
        [
            new(AssetProcessIntelligenceScopeKind.Location, locationId, Active: true),
            new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId)
        ]));
        var service = CreateExchange(registry);

        var parsed = service.ParseJson(service.ExportJson());

        Assert.Equal(24, parsed.SchemaVersion);
        Assert.Equal(registry.Snapshot().Version, parsed.AssetProcessIntelligence?.Version);
        Assert.Equal(registry.Snapshot().Scopes, parsed.AssetProcessIntelligence?.Scopes);
    }

    [Fact]
    public void ImportDisablesPersistedActivationUnlessItIsLocalRevisionHydration()
    {
        var scopeId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
        var incoming = new AssetProcessIntelligenceEngineeringDto(Scopes:
        [new(AssetProcessIntelligenceScopeKind.Equipment, scopeId, Active: true)]);
        var externalRegistry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var external = CreateExchange(externalRegistry);
        var project = Package(incoming, equipment:
            [new EquipmentEngineeringDto(scopeId, "Plant.P01", "Pump P01")]);
        var externalResult = external.Apply(project, ImportMode.CreateAndUpdate);

        Assert.Empty(externalResult.Issues);
        Assert.False(Assert.Single(externalRegistry.Snapshot().Scopes!).Active);

        var restoredRegistry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var restored = CreateExchange(restoredRegistry);
        var restoreResult = restored.Apply(
            project,
            ImportMode.CreateAndUpdate,
            new EngineeringImportContext(PreserveActivatedIntelligenceScopes: true));

        Assert.Empty(restoreResult.Issues);
        Assert.True(Assert.Single(restoredRegistry.Snapshot().Scopes!).Active);
    }

    [Fact]
    public void PreviewRejectsUnknownAndDuplicateScopeReferences()
    {
        var locations = new InMemoryEngineeringAssetRegistry();
        locations.UpsertLocation(new LocationEngineeringDto(
            Guid.Parse("a3000000-0000-0000-0000-000000000001"), "Boiler House"));
        var equipmentId = Guid.Parse("a3000000-0000-0000-0000-000000000002");
        locations.UpsertEquipment(new EquipmentEngineeringDto(equipmentId, "Plant.P01", "Pump P01"));
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry(), locations);
        var package = Package(new AssetProcessIntelligenceEngineeringDto(Scopes:
        [
            new(AssetProcessIntelligenceScopeKind.Location, locations.SnapshotLocations().Single().Id!.Value),
            new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId),
            new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId),
            new(AssetProcessIntelligenceScopeKind.Location, Guid.NewGuid())
        ]),
        equipment: [new EquipmentEngineeringDto(equipmentId, "Plant.P01", "Pump P01")],
        locations: [locations.SnapshotLocations().Single()]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "INTELLIGENCE_SCOPE_DUPLICATE");
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "INTELLIGENCE_SCOPE_RESOURCE_NOT_FOUND");
    }

    [Fact]
    public void ManagerCapabilityRequiresStableEquipmentOrLocationScopeAndDoesNotInheritGlobally()
    {
        var locationId = Guid.Parse("a4000000-0000-0000-0000-000000000001");
        var locationNode = Guid.Parse("a4000000-0000-0000-0000-000000000002");
        Assert.True(SecurityScopeGraph.TryCreate(
            [new(locationNode, "plant-east", "Plant East", SecurityScopeNodeKind.Location, ResourceId: locationId)],
            out var graph,
            out _));
        var role = new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "intelligence-manager",
            "Intelligence Manager",
            Grants:
            [new(SecurityCapability.AssetProcessIntelligenceManage,
                new AuthorizationScopeEngineeringDto(ScopeNodeId: locationNode))]);
        var issues = SecurityPolicyEngineeringValidator.Validate(role, graph);
        Assert.Empty(issues);

        var policies = new InMemoryCapabilityAuthorizationService(
        [SecurityPolicyCompiler.Compile(role)]);
        var principal = new SecurityPrincipal("manager-1", "Manager", ["intelligence-manager"]);
        var allowed = policies.Evaluate(
            principal,
            SecurityCapability.AssetProcessIntelligenceManage,
            graph!.Enrich(new AuthorizationResource(
                ResourceKind: AuthorizationResourceKind.Location,
                ResourceId: locationId)));
        var deniedForOtherLocation = policies.Evaluate(
            principal,
            SecurityCapability.AssetProcessIntelligenceManage,
            graph.Enrich(new AuthorizationResource(
                ResourceKind: AuthorizationResourceKind.Location,
                ResourceId: Guid.NewGuid())));
        var invalidGlobal = SecurityPolicyEngineeringValidator.Validate(role with
        {
            Grants = [new(SecurityCapability.AssetProcessIntelligenceManage)]
        }, graph);

        Assert.True(allowed.Allowed);
        Assert.False(deniedForOtherLocation.Allowed);
        Assert.Contains(invalidGlobal, issue => issue.Code == "INTELLIGENCE_MANAGER_SCOPE_REQUIRED");
    }

    private static EngineeringExchangeService CreateExchange(
        IAssetProcessIntelligenceEngineeringRegistry intelligence,
        InMemoryEngineeringAssetRegistry? assets = null)
    {
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets ?? new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new Scada.Engineering.Commands.InMemoryCommandEngineeringRegistry(),
            new Scada.Engineering.Gateways.InMemoryGatewayEngineeringRegistry(),
            assetProcessIntelligence: intelligence);
    }

    private static EngineeringPackage Package(
        AssetProcessIntelligenceEngineeringDto configuration,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment = null,
        IReadOnlyCollection<LocationEngineeringDto>? locations = null) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment: equipment,
            Locations: locations,
            AssetProcessIntelligence: configuration);
}
