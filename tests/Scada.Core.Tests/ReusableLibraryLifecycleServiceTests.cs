using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryLifecycleServiceTests
{
    [Fact]
    public void NewerVersion_ReportsUpdateAvailable_CompareUpgradePreservesInstanceIdentity()
    {
        var libraryId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var v1 = CreateTemplateLibrary(
            libraryId,
            "1.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.pump",
                "Pump v1",
                Properties: new Dictionary<string, string> { ["ratedPower"] = "10" }));
        var v2 = CreateTemplateLibrary(
            libraryId,
            "2.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.pump",
                "Pump v2",
                Properties: new Dictionary<string, string>
                {
                    ["ratedPower"] = "15",
                    ["family"] = "centrifugal"
                }));

        using var target = CreateHarness();
        Incorporate(target, v1, templateId);

        var incorporatedV1 = Assert.IsType<EquipmentTemplateEngineeringDto>(
            target.Assets.FindTemplate(templateId));
        Assert.True(ReusableLibraryProvenance.TryRead(
            incorporatedV1.Metadata,
            out var initialProvenance));
        Assert.Equal(libraryId, initialProvenance.SourceLibraryId);
        Assert.Equal(templateId, initialProvenance.SourceResourceId);
        Assert.Equal("1.0.0", initialProvenance.SourceVersion);
        Assert.False(string.IsNullOrWhiteSpace(initialProvenance.SourceContentHash));

        var equipmentId = Guid.NewGuid();
        target.Assets.UpsertEquipment(new EquipmentEngineeringDto(
            equipmentId,
            "Plant.P01",
            "P01",
            TemplateKey: "template.lifecycle.pump",
            TemplateId: templateId,
            Properties: new Dictionary<string, string> { ["instanceOverride"] = "kept" }));

        var status = target.Lifecycle.Evaluate(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            v2);

        Assert.Equal(ReusableLibraryUpdateState.UpdateAvailable, status.Update.State);
        Assert.Equal(libraryId, status.Update.Source.SourceLibraryId);
        Assert.Equal(templateId, status.Update.Source.SourceResourceId);
        Assert.Equal("1.0.0", status.Update.Source.SourceVersion);

        var compare = target.Lifecycle.Compare(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            v2);
        Assert.Contains(compare.Changes, change => change.Property == "name");
        Assert.Contains(compare.Changes, change => change.Property == "properties");

        var plan = target.Lifecycle.PreviewUpgrade(
            v2,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));

        Assert.True(plan.CanApply);
        Assert.Contains(plan.ResourceStates, state =>
            state.ProjectResourceId == templateId &&
            state.Update.State == ReusableLibraryUpdateState.UpdateAvailable);

        var repeatedPlan = target.Lifecycle.PreviewUpgrade(
            v2,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));
        Assert.Equal(plan.TargetFingerprint, repeatedPlan.TargetFingerprint);
        Assert.Equal(
            plan.ResourceStates.Select(state => (state.Kind, state.ProjectResourceId, state.Update.State)),
            repeatedPlan.ResourceStates.Select(state => (state.Kind, state.ProjectResourceId, state.Update.State)));
        Assert.Equal(
            plan.CanonicalPreview.Items.Select(item => (item.EntityKind, item.EntityKey, item.Operation)),
            repeatedPlan.CanonicalPreview.Items.Select(item => (item.EntityKind, item.EntityKey, item.Operation)));

        var result = target.Lifecycle.ApplyUpgrade(plan);

        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var upgraded = Assert.IsType<EquipmentTemplateEngineeringDto>(
            target.Assets.FindTemplate(templateId));
        Assert.Equal("Pump v2", upgraded.Name);
        Assert.Equal("15", upgraded.Properties!["ratedPower"]);

        var equipment = Assert.IsType<EquipmentEngineeringDto>(
            target.Assets.FindEquipment(equipmentId));
        Assert.Equal(templateId, equipment.TemplateId);
        Assert.Equal("kept", equipment.Properties!["instanceOverride"]);

        var after = target.Lifecycle.Evaluate(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            v2);
        Assert.Equal(ReusableLibraryUpdateState.UpToDate, after.Update.State);
        Assert.Equal("2.0.0", after.Update.Source.SourceVersion);
    }

    [Fact]
    public void LocalDivergence_BlocksUpgradeBeforeMutation()
    {
        var libraryId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var v1 = CreateTemplateLibrary(
            libraryId,
            "1.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.local",
                "Original"));
        var v2 = CreateTemplateLibrary(
            libraryId,
            "2.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.local",
                "Source v2"));

        using var target = CreateHarness();
        Incorporate(target, v1, templateId);

        var current = target.Assets.FindTemplate(templateId)!;
        target.Assets.UpsertTemplate(current with { Name = "Local edit" });

        var status = target.Lifecycle.Evaluate(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            v2);
        Assert.Equal(ReusableLibraryUpdateState.LocallyModified, status.Update.State);

        var plan = target.Lifecycle.PreviewUpgrade(
            v2,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));

        Assert.False(plan.CanApply);
        var result = target.Lifecycle.ApplyUpgrade(plan);
        Assert.Contains(result.Issues, issue => issue.Code == "LIBRARY_UPGRADE_BLOCKED");
        Assert.Equal("Local edit", target.Assets.FindTemplate(templateId)!.Name);
    }

    [Fact]
    public void ForkDetach_PreservesContentAndRemovesUpdateRelationship()
    {
        var libraryId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var v1 = CreateTemplateLibrary(
            libraryId,
            "1.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.detach",
                "Detach Me",
                Properties: new Dictionary<string, string> { ["family"] = "pump" }));

        using var target = CreateHarness();
        Incorporate(target, v1, templateId);

        var detached = target.Lifecycle.ForkDetach(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId);

        Assert.Equal(ReusableLibraryUpdateState.Incompatible, detached.Update.State);
        var project = target.Assets.FindTemplate(templateId)!;
        Assert.Equal("Detach Me", project.Name);
        Assert.Equal("pump", project.Properties!["family"]);
        Assert.False(ReusableLibraryProvenance.TryRead(project.Metadata, out _));

        var status = target.Lifecycle.Evaluate(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            null);
        Assert.Equal(ReusableLibraryUpdateState.Incompatible, status.Update.State);
    }

    [Fact]
    public void MissingAssociatedSource_ReportsSourceMissing()
    {
        var libraryId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var v1 = CreateTemplateLibrary(
            libraryId,
            "1.0.0",
            new EquipmentTemplateEngineeringDto(
                templateId,
                "template.lifecycle.missing",
                "Missing Source"));

        using var target = CreateHarness();
        Incorporate(target, v1, templateId);

        var status = target.Lifecycle.Evaluate(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId,
            null);

        Assert.Equal(ReusableLibraryUpdateState.SourceMissing, status.Update.State);
    }

    private static byte[] CreateTemplateLibrary(
        Guid libraryId,
        string version,
        EquipmentTemplateEngineeringDto template)
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        assets.UpsertTemplate(template);
        var packages = new ReusableLibraryPackageService(assets, visualAssets);
        return packages.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Lifecycle Library",
            version,
            [new(ReusableLibraryResourceKinds.EquipmentTemplate, template.Id!.Value)]));
    }

    private static void Incorporate(Harness target, byte[] libraryBytes, Guid templateId)
    {
        var plan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));
        Assert.True(plan.Preview.CanApply);
        var result = target.Exchange.Apply(
            plan.Engineering,
            ImportMode.CreateOnly,
            plan.ImportContext);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
    }

    private static Harness CreateHarness()
    {
        var events = new InMemoryScadaEventBus();
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(events);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var gateways = new InMemoryGatewayEngineeringRegistry();
        var scripts = new InMemoryScriptEngineeringRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            security,
            commands,
            gateways,
            scripts,
            visualAssets);
        var packages = new ReusableLibraryPackageService(
            assets,
            visualAssets,
            scripts,
            views);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            assets,
            visualAssets,
            exchange,
            scripts,
            views);
        var lifecycle = new ReusableLibraryLifecycleService(
            packages,
            assets,
            views,
            scripts,
            visualAssets,
            exchange);

        return new Harness(
            alarms,
            assets,
            exchange,
            incorporation,
            lifecycle);
    }

    private sealed class Harness(
        InMemoryAlarmEngine alarms,
        InMemoryEngineeringAssetRegistry assets,
        EngineeringExchangeService exchange,
        ReusableLibraryIncorporationService incorporation,
        ReusableLibraryLifecycleService lifecycle) : IDisposable
    {
        public InMemoryEngineeringAssetRegistry Assets { get; } = assets;
        public EngineeringExchangeService Exchange { get; } = exchange;
        public ReusableLibraryIncorporationService Incorporation { get; } = incorporation;
        public ReusableLibraryLifecycleService Lifecycle { get; } = lifecycle;

        public void Dispose() => alarms.Dispose();
    }
}
