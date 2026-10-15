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
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Core.Tests;

public sealed class AssetProcessIntelligenceEngineeringTests
{
    [Fact]
    public void Schema26ExportsAndRestoresStableEligibleScopesAndActivationState()
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

        Assert.Equal(26, parsed.SchemaVersion);
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
        Assert.Equal(26, parsed.SchemaVersion);
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

    [Fact]
    public void Schema26RoundTripPreservesControlledUnitTypedRoleAndLegacyFreeText()
    {
        var tagId = Guid.Parse("a5100000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("a5100000-0000-0000-0000-000000000002");
        var signalId = Guid.Parse("a5100000-0000-0000-0000-000000000003");
        var tag = new TagDefinition(
            tagId, "Pump current", "Plant.P01.Current", TagDataType.Double, null, "Amps",
            null, true, ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere"));
        var tags = new InMemoryTagRegistry();
        tags.Register(tag);
        var intelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var semanticBinding = new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
            1, "pump.electrical_current", CurrentBasis: "line_rms");
        intelligence.Replace(new AssetProcessIntelligenceEngineeringDto(Version: 2, Profiles:
        [new(profileId, "pump", "Pump", [new(signalId, "current", "Legacy Current Text", tagId, SemanticBinding: semanticBinding)])]));
        var service = CreateExchange(intelligence, tags: tags);

        var parsed = service.ParseJson(service.ExportJson());
        var restoredIntelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        var restoredService = CreateExchange(restoredIntelligence, tags: new InMemoryTagRegistry());
        var applied = restoredService.Apply(parsed, ImportMode.CreateAndUpdate);

        Assert.Equal(26, parsed.SchemaVersion);
        Assert.Empty(applied.Issues);
        Assert.Equal(tag.ControlledEngineeringUnit, Assert.Single(parsed.Tags).ControlledEngineeringUnit);
        var restoredTag = Assert.Single(restoredService.ExportPackage().Tags);
        Assert.Equal(tag.ControlledEngineeringUnit, restoredTag.ControlledEngineeringUnit);
        var restoredSignal = Assert.Single(Assert.Single(restoredIntelligence.Snapshot().Profiles!).Signals!);
        Assert.Equal("Legacy Current Text", restoredSignal.SignalRole);
        Assert.Equal(semanticBinding, restoredSignal.SemanticBinding);
    }

    [Fact]
    public void LegacySchema25UpdateThatDropsTypedAnnotationsFailsPreviewAndApplyWithoutMutation()
    {
        var tagId = Guid.Parse("a5200000-0000-0000-0000-000000000001");
        var profileId = Guid.Parse("a5200000-0000-0000-0000-000000000002");
        var signalId = Guid.Parse("a5200000-0000-0000-0000-000000000003");
        var controlledUnit = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var tags = new InMemoryTagRegistry();
        tags.Register(new TagDefinition(tagId, "Current", "Plant.P01.Current", TagDataType.Double, null, "A", null, true,
            ControlledEngineeringUnit: controlledUnit));
        var intelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        intelligence.Replace(new AssetProcessIntelligenceEngineeringDto(Version: 2, Profiles:
        [new(profileId, "pump", "Pump", [new(signalId, "current", "current", tagId,
            SemanticBinding: new(1, "pump.electrical_current", CurrentBasis: "line_rms"))])]));
        var service = CreateExchange(intelligence, tags: tags);
        var package = Package(new AssetProcessIntelligenceEngineeringDto(Version: 2, Profiles:
            [new(profileId, "pump", "Pump", [new(signalId, "current", "current", tagId)])]),
            tags: [new TagEngineeringDto(tagId, "Current", "Plant.P01.Current", TagDataType.Double, EngineeringUnit: "A")])
            with { SchemaVersion = 25 };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var applied = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "TAG_CONTROLLED_UNIT_ANNOTATION_CONFLICT");
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT");
        Assert.Contains(applied.Issues, issue => issue.Code == "TAG_CONTROLLED_UNIT_ANNOTATION_CONFLICT");
        Assert.Equal(controlledUnit, tags.Snapshot().Single().ControlledEngineeringUnit);
        Assert.NotNull(Assert.Single(intelligence.Snapshot().Profiles!.Single().Signals!).SemanticBinding);
    }

    [Fact]
    public void Schema26UpdateCanExplicitlyClearControlledUnitAnnotation()
    {
        var tagId = Guid.Parse("a5250000-0000-0000-0000-000000000001");
        var controlledUnit = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var tags = new InMemoryTagRegistry();
        tags.Register(new TagDefinition(tagId, "Current", "Plant.P01.Current", TagDataType.Double, null, "A", null, true,
            ControlledEngineeringUnit: controlledUnit));
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry(), tags: tags);
        var package = Package(new AssetProcessIntelligenceEngineeringDto(),
            tags: [new TagEngineeringDto(tagId, "Current", "Plant.P01.Current", TagDataType.Double, EngineeringUnit: "A")]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var applied = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Equal(26, package.SchemaVersion);
        Assert.True(preview.CanApply);
        Assert.DoesNotContain(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "TAG_CONTROLLED_UNIT_ANNOTATION_CONFLICT");
        Assert.Empty(applied.Issues);
        Assert.Null(Assert.Single(tags.Snapshot()).ControlledEngineeringUnit);
    }

    [Fact]
    public void LegacyTagsCsvUpdateWithoutControlledUnitConflictsWithoutMutation()
    {
        var tagId = Guid.Parse("a5260000-0000-0000-0000-000000000001");
        var controlledUnit = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var tags = new InMemoryTagRegistry();
        tags.Register(new TagDefinition(tagId, "Current", "Plant.P01.Current", TagDataType.Double, null, "A", null, true,
            ControlledEngineeringUnit: controlledUnit));
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry(), tags: tags);
        var package = service.ParseTagsCsv("Id,Path,Name,DataType,Unit\n" +
            $"{tagId:D},Plant.P01.Current,Current,Double,A\n");

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var applied = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Equal(25, package.SchemaVersion);
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "TAG_CONTROLLED_UNIT_ANNOTATION_CONFLICT");
        Assert.Contains(applied.Issues, issue => issue.Code == "TAG_CONTROLLED_UNIT_ANNOTATION_CONFLICT");
        Assert.Equal(controlledUnit, Assert.Single(tags.Snapshot()).ControlledEngineeringUnit);
    }

    [Fact]
    public void Schema26TagsCsvBlankControlledUnitColumnExplicitlyClearsAnnotation()
    {
        var tagId = Guid.Parse("a5270000-0000-0000-0000-000000000001");
        var controlledUnit = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var tags = new InMemoryTagRegistry();
        tags.Register(new TagDefinition(tagId, "Current", "Plant.P01.Current", TagDataType.Double, null, "A", null, true,
            ControlledEngineeringUnit: controlledUnit));
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry(), tags: tags);
        var header = service.ExportTagsCsv().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        var columns = header.Split(',');
        var row = new string[columns.Length];
        var index = columns.Select((name, position) => (name, position))
            .ToDictionary(item => item.name, item => item.position, StringComparer.OrdinalIgnoreCase);
        row[index["Id"]] = tagId.ToString("D");
        row[index["Path"]] = "Plant.P01.Current";
        row[index["Name"]] = "Current";
        row[index["DataType"]] = nameof(TagDataType.Double);
        row[index["Unit"]] = "A";
        row[index["ReadOnly"]] = bool.TrueString;
        var package = service.ParseTagsCsv(header + "\r\n" + string.Join(',', row));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var applied = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Equal(26, package.SchemaVersion);
        Assert.Null(Assert.Single(package.Tags).ControlledEngineeringUnit);
        Assert.True(preview.CanApply);
        Assert.Empty(applied.Issues);
        Assert.Null(Assert.Single(tags.Snapshot()).ControlledEngineeringUnit);
    }

    [Theory]
    [InlineData(50d, "unit.ratio.percent", 0.5d)]
    [InlineData(3.25d, "unit.electric_current.ampere", 3.25d)]
    [InlineData(60d, "unit.frequency.hertz", 60d)]
    [InlineData(1.2d, "unit.pressure.bar", 120000d)]
    [InlineData(3600d, "unit.volumetric_flow.cubic_metre_per_hour", 1d)]
    public void ControlledUnitConversionsUseExplicitInvariantScales(double value, string code, double expected)
    {
        var success = ControlledEngineeringUnitCatalog.TryConvertToCanonical(
            value,
            new ControlledEngineeringUnitReference(1, code),
            out var actual);

        Assert.True(success);
        Assert.Equal(expected, actual, precision: 10);
    }

    [Fact]
    public void ControlledUnitConversionRejectsUnknownVersionCodeAndNonFiniteInputs()
    {
        Assert.False(ControlledEngineeringUnitCatalog.TryConvertToCanonical(
            1d, new ControlledEngineeringUnitReference(2, "unit.pressure.bar"), out _));
        Assert.False(ControlledEngineeringUnitCatalog.TryConvertToCanonical(
            1d, new ControlledEngineeringUnitReference(1, "unit.pressure.psi"), out _));
        Assert.False(ControlledEngineeringUnitCatalog.TryConvertToCanonical(
            double.PositiveInfinity, new ControlledEngineeringUnitReference(1, "unit.pressure.bar"), out _));
        Assert.False(ControlledEngineeringUnitCatalog.TryConvertToCanonical(
            double.MaxValue, new ControlledEngineeringUnitReference(1, "unit.pressure.bar"), out _));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("de-DE")]
    public void ControlledUnitConversionIsLocaleIndependent(string locale)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            Assert.True(ControlledEngineeringUnitCatalog.TryConvertToCanonical(
                1.25d,
                new ControlledEngineeringUnitReference(1, "unit.pressure.bar"),
                out var canonical));
            Assert.Equal(125000d, canonical);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void SemanticCatalogAcceptsFourFullyQualifiedRolesAndRejectsIncompleteUnknownOrWrongDimension()
    {
        var cases = new[]
        {
            ("pump.electrical_current", new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(1, "pump.electrical_current", CurrentBasis: "line_rms"), "unit.electric_current.ampere"),
            ("pump.drive_frequency", new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(1, "pump.drive_frequency", SignalKind: "measured_output", FrequencyReference: "drive_electrical"), "unit.frequency.hertz"),
            ("pump.discharge_pressure", new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(1, "pump.discharge_pressure", PressureReference: "gauge"), "unit.pressure.bar"),
            ("pump.discharge_volumetric_flow", new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(1, "pump.discharge_volumetric_flow", FlowDirection: "pump_to_process"), "unit.volumetric_flow.cubic_metre_per_hour")
        };
        var tags = cases.Select((item, index) => new TagEngineeringDto(
            Guid.Parse($"a5300000-0000-0000-0000-{index + 1:000000000000}"), item.Item1,
            $"Plant.P01.{item.Item1.Split('.').Last()}", TagDataType.Double,
            ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, item.Item3))).ToArray();
        var signals = cases.Select((item, index) => new AssetProcessIntelligenceSignalEngineeringDto(
            Guid.Parse($"a5310000-0000-0000-0000-{index + 1:000000000000}"),
            item.Item1.Split('.').Last(), "legacy role text", tags[index].Id!.Value,
            SemanticBinding: item.Item2)).ToArray();
        var configuration = new AssetProcessIntelligenceEngineeringDto(Version: 2,
            Profiles: [new(Guid.Parse("a5320000-0000-0000-0000-000000000001"), "pump", "Pump", signals)]);

        Assert.Empty(AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null, tags));

        var incomplete = cases[0].Item2 with { CurrentBasis = null };
        Assert.Contains(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(incomplete, Guid.NewGuid()),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_QUALIFIER_INCOMPLETE");
        Assert.Contains(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(
            new(1, "pump.unknown", CurrentBasis: "line_rms"), Guid.NewGuid()),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_ROLE_UNSUPPORTED");
        Assert.Contains(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(
            new(2, "pump.electrical_current", CurrentBasis: "line_rms"), Guid.NewGuid()),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_CATALOG_VERSION_UNSUPPORTED");

        var wrongDimensionTags = tags.ToArray();
        wrongDimensionTags[0] = wrongDimensionTags[0] with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(1, "unit.pressure.bar")
        };
        Assert.Contains(AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null, wrongDimensionTags),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_DIMENSION_MISMATCH");
        var nonNumericTags = tags.ToArray();
        nonNumericTags[0] = nonNumericTags[0] with { DataType = TagDataType.Boolean };
        Assert.Contains(AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null, nonNumericTags),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_TAG_TYPE_UNSUPPORTED");
        Assert.Contains(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(
            new(1, "pump.electrical_current", CurrentBasis: "line"), Guid.NewGuid()),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_QUALIFIER_UNSUPPORTED");
        Assert.Contains(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(
            new(1, "pump.electrical_current", CurrentBasis: "line_rms", PressureReference: "gauge"), Guid.NewGuid()),
            issue => issue.Code == "INTELLIGENCE_SEMANTIC_QUALIFIER_NOT_APPLICABLE");
    }

    [Theory]
    [InlineData("pump.electrical_current", "currentBasis", "line_rms")]
    [InlineData("pump.electrical_current", "currentBasis", "phase_rms")]
    [InlineData("pump.electrical_current", "currentBasis", "dc_bus")]
    [InlineData("pump.drive_frequency", "signalKind", "measured_output")]
    [InlineData("pump.drive_frequency", "signalKind", "command_setpoint")]
    [InlineData("pump.drive_frequency", "frequencyReference", "drive_electrical")]
    [InlineData("pump.drive_frequency", "frequencyReference", "shaft_rotational")]
    [InlineData("pump.discharge_pressure", "pressureReference", "gauge")]
    [InlineData("pump.discharge_pressure", "pressureReference", "absolute")]
    [InlineData("pump.discharge_volumetric_flow", "flowDirection", "pump_to_process")]
    [InlineData("pump.discharge_volumetric_flow", "flowDirection", "process_to_pump")]
    [InlineData("pump.discharge_volumetric_flow", "flowDirection", "bidirectional_signed")]
    [InlineData("pump.discharge_volumetric_flow", "flowDirection", "bidirectional_magnitude")]
    public void SemanticCatalogAcceptsEveryApprovedQualifier(string role, string qualifier, string value)
    {
        var binding = role switch
        {
            "pump.electrical_current" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(1, role, CurrentBasis: value),
            "pump.drive_frequency" when qualifier == "signalKind" => new(1, role, SignalKind: value, FrequencyReference: "drive_electrical"),
            "pump.drive_frequency" => new(1, role, SignalKind: "measured_output", FrequencyReference: value),
            "pump.discharge_pressure" => new(1, role, PressureReference: value),
            "pump.discharge_volumetric_flow" => new(1, role, FlowDirection: value),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        Assert.Empty(AssetProcessIntelligenceSemanticCatalog.ValidateBinding(binding, Guid.NewGuid()));
    }

    [Fact]
    public void TagsCsvRoundTripsTypedUnitAndLegacyCsvRemainsSchema25()
    {
        var tagId = Guid.Parse("a5400000-0000-0000-0000-000000000001");
        var tags = new InMemoryTagRegistry();
        tags.Register(new TagDefinition(tagId, "Flow", "Plant.P01.Flow", TagDataType.Double, null, "m³/h", null, true,
            ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, "unit.volumetric_flow.cubic_metre_per_hour")));
        var service = CreateExchange(new InMemoryAssetProcessIntelligenceEngineeringRegistry(), tags: tags);

        var parsedCurrent = service.ParseTagsCsv(service.ExportTagsCsv());
        var parsedLegacy = service.ParseTagsCsv("Id,Path,Name,DataType,Unit\n" +
            $"{tagId:D},Plant.P01.Flow,Flow,Double,m³/h\n");

        Assert.Equal(26, parsedCurrent.SchemaVersion);
        Assert.Equal(new ControlledEngineeringUnitReference(1, "unit.volumetric_flow.cubic_metre_per_hour"),
            Assert.Single(parsedCurrent.Tags).ControlledEngineeringUnit);
        Assert.Equal(25, parsedLegacy.SchemaVersion);
        Assert.Null(Assert.Single(parsedLegacy.Tags).ControlledEngineeringUnit);
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
