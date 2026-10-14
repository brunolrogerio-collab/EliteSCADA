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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Core.Tests;

public sealed class AssetProcessIntelligenceEngineeringTests
{
    [Fact]
    public void Schema25ExportsAndRestoresStableEligibleScopesAndActivationState()
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

        Assert.Equal(25, parsed.SchemaVersion);
        Assert.Equal(registry.Snapshot().Version, parsed.AssetProcessIntelligence?.Version);
        Assert.Equal(registry.Snapshot().Scopes, parsed.AssetProcessIntelligence?.Scopes);
    }

    [Fact]
    public void Schema24ScopeOnlyConfigurationRemainsReadableAndVersion2RequiresSchema25()
    {
        var id = Guid.Parse("a1500000-0000-0000-0000-000000000001");
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry());
        var schema24 = Package(new AssetProcessIntelligenceEngineeringDto(Scopes:
            [new(AssetProcessIntelligenceScopeKind.Equipment, id)]),
            equipment: [new EquipmentEngineeringDto(id, "Plant.P01", "Pump P01")]) with { SchemaVersion = 24 };

        var parsedV1 = service.ParseJson(JsonSerializer.Serialize(schema24, CompatibilityJsonOptions()));
        Assert.Equal(24, parsedV1.SchemaVersion);
        Assert.Equal(1, parsedV1.AssetProcessIntelligence?.Version);
        Assert.Single(parsedV1.AssetProcessIntelligence!.Scopes!);

        var v2 = schema24 with
        {
            AssetProcessIntelligence = new AssetProcessIntelligenceEngineeringDto(Version: 2)
        };
        Assert.Throws<InvalidDataException>(() => service.ParseJson(JsonSerializer.Serialize(v2, CompatibilityJsonOptions())));
        Assert.False(service.Preview(v2, ImportMode.CreateAndUpdate).CanApply);
    }

    [Fact]
    public void Version2ProfilesAssignmentsAndOrderedStagesRoundTripDeterministicallyWithoutSamples()
    {
        var tagId = Guid.Parse("a1600000-0000-0000-0000-000000000001");
        var equipmentId = Guid.Parse("a1600000-0000-0000-0000-000000000002");
        var profileId = Guid.Parse("a1600000-0000-0000-0000-000000000003");
        var assignmentId = Guid.Parse("a1600000-0000-0000-0000-000000000004");
        var stageId = Guid.Parse("a1600000-0000-0000-0000-000000000005");
        var registry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var service = CreateExchange(registry);
        var configuration = new AssetProcessIntelligenceEngineeringDto(
            Version: 2,
            Scopes: [new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId, Active: true)],
            Profiles: [new(profileId, "pump-main", "Main pump",
                [new(Guid.Parse("a1600000-0000-0000-0000-000000000006"), "temperature", "process", tagId, Required: false)])],
            Assignments: [new(assignmentId, equipmentId, profileId)],
            ProcessStages: [new(stageId, "treatment", "Treatment", 0, [new(assignmentId, 0)])]);
        var package = Package(configuration,
            equipment: [new EquipmentEngineeringDto(equipmentId, "Plant.P01", "Pump P01")],
            tags: [new TagEngineeringDto(tagId, "Temperature", "Plant.P01.Temperature", TagDataType.Double, EngineeringUnit: "°C")]);

        var result = service.Apply(package, ImportMode.CreateAndUpdate);
        var firstExport = service.ExportPackage();
        var parsed = service.ParseJson(service.ExportJson());
        var restoredRegistry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var restoredService = CreateExchange(restoredRegistry);
        var restored = restoredService.Apply(parsed, ImportMode.CreateAndUpdate);

        Assert.Empty(result.Issues);
        Assert.Empty(restored.Issues);
        Assert.Equal(25, parsed.SchemaVersion);
        Assert.False(Assert.Single(registry.Snapshot().Scopes!).Active);
        Assert.Equal(JsonSerializer.Serialize(configuration.Profiles), JsonSerializer.Serialize(registry.Snapshot().Profiles));
        Assert.Equal(JsonSerializer.Serialize(configuration.Assignments), JsonSerializer.Serialize(registry.Snapshot().Assignments));
        Assert.Equal(JsonSerializer.Serialize(configuration.ProcessStages), JsonSerializer.Serialize(registry.Snapshot().ProcessStages));
        Assert.Equal(
            JsonSerializer.Serialize(firstExport.AssetProcessIntelligence),
            JsonSerializer.Serialize(restoredService.ExportPackage().AssetProcessIntelligence));
        Assert.DoesNotContain("sampleValue", service.ExportJson(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentValue", service.ExportJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewRejectsMissingAndAmbiguousStage2ReferencesWithoutPartialMutation()
    {
        var equipmentId = Guid.Parse("a1700000-0000-0000-0000-000000000001");
        var tagId = Guid.Parse("a1700000-0000-0000-0000-000000000002");
        var profileId = Guid.Parse("a1700000-0000-0000-0000-000000000003");
        var assignmentId = Guid.Parse("a1700000-0000-0000-0000-000000000004");
        var registry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var tags = new InMemoryTagRegistry();
        var service = CreateExchange(registry, assets, tags);
        var configuration = new AssetProcessIntelligenceEngineeringDto(
            Version: 2,
            Profiles:
            [
                new(profileId, "pump", "Pump", [new(Guid.NewGuid(), "flow", "process", Guid.NewGuid())]),
                new(profileId, "pump-copy", "Duplicate profile", [])
            ],
            Assignments: [new(assignmentId, Guid.NewGuid(), profileId)],
            ProcessStages: [new(Guid.NewGuid(), "step", "Step", -1,
                [new(assignmentId, 0), new(Guid.NewGuid(), 0)])]);
        var package = Package(configuration,
            equipment: [new EquipmentEngineeringDto(equipmentId, "Plant.P01", "Pump P01")],
            tags: [new TagEngineeringDto(tagId, "Flow", "Plant.P01.Flow", TagDataType.Double)]);

        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.NotEmpty(result.Issues);
        Assert.Empty(tags.Snapshot());
        Assert.Empty(assets.SnapshotEquipment());
        Assert.Empty(registry.Snapshot().Profiles!);
        Assert.Contains(result.Issues, issue => issue.Code == "INTELLIGENCE_PROFILE_ID_DUPLICATE");
        Assert.Contains(result.Issues, issue => issue.Code == "INTELLIGENCE_SIGNAL_TAG_NOT_FOUND");
        Assert.Contains(result.Issues, issue => issue.Code == "INTELLIGENCE_ASSIGNMENT_EQUIPMENT_NOT_FOUND");
        Assert.Contains(result.Issues, issue => issue.Code == "INTELLIGENCE_PROCESS_STAGE_ORDER_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "INTELLIGENCE_PROCESS_STAGE_ASSIGNMENT_NOT_FOUND");
    }

    [Fact]
    public void StableTagIdKeepsSignalReferenceAcrossTagRename()
    {
        var tagId = Guid.Parse("a1800000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("a1800000-0000-0000-0000-000000000002");
        var tagRegistry = new InMemoryTagRegistry();
        tagRegistry.Register(new TagDefinition(tagId, "Old temperature", "Plant.P01.Temp", TagDataType.Double, null, "°C", null, true));
        var intelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        intelligence.Replace(new AssetProcessIntelligenceEngineeringDto(Version: 2, Profiles:
            [new(profileId, "pump", "Pump", [new(Guid.NewGuid(), "temperature", "process", tagId)])]));
        var service = CreateExchange(intelligence, tags: tagRegistry);
        var renamed = Package(new AssetProcessIntelligenceEngineeringDto(Version: 2, Profiles:
            [new(profileId, "pump", "Pump", intelligence.Snapshot().Profiles!.Single().Signals)]),
            tags: [new TagEngineeringDto(tagId, "New temperature", "Plant.P01.TempNew", TagDataType.Double, EngineeringUnit: "°C")]);

        var preview = service.Preview(renamed, ImportMode.CreateAndUpdate);
        var result = service.Apply(renamed, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.Equal(tagId, Assert.Single(intelligence.Snapshot().Profiles!.Single().Signals!).TagId);
        Assert.Equal("New temperature", service.ExportPackage().Tags.Single().Name);
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
        InMemoryEngineeringAssetRegistry? assets = null,
        ITagRegistry? tags = null)
    {
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            tags ?? new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets ?? new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new Scada.Engineering.Commands.InMemoryCommandEngineeringRegistry(),
            new Scada.Engineering.Gateways.InMemoryGatewayEngineeringRegistry(),
            assetProcessIntelligence: intelligence);
    }

    private static JsonSerializerOptions CompatibilityJsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private static EngineeringPackage Package(
        AssetProcessIntelligenceEngineeringDto configuration,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment = null,
        IReadOnlyCollection<LocationEngineeringDto>? locations = null,
        IReadOnlyCollection<TagEngineeringDto>? tags = null) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tags ?? Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment: equipment,
            Locations: locations,
            AssetProcessIntelligence: configuration);
}
