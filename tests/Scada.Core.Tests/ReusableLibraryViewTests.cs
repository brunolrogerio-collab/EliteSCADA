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
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryViewTests
{
    [Fact]
    public void ScreenAndPopup_ExportSelectiveIncorporationAndProjectRoundTrip_AreSelfContained()
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceViews = new InMemoryEngineeringViewRegistry();
        var sourceScripts = new InMemoryScriptEngineeringRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var popupId = Guid.NewGuid();

        sourceAssets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.view.portable",
            "Portable View Template"));
        sourceAssets.UpsertDynamo(new DynamoEngineeringDto(
            dynamoId,
            "dynamo.view.portable",
            "Portable View Dynamo"));
        sourceViews.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "screen.view.portable",
            "Portable Screen",
            Route: "/portable",
            Elements:
            [new VisualElementEngineeringDto(
                "dynamo",
                "dynamo",
                DynamoKey: "dynamo.view.portable")],
            Metadata: new Dictionary<string, string> { ["family"] = "screen" }));
        sourceViews.UpsertPopup(new PopupEngineeringDto(
            popupId,
            "popup.view.portable",
            "Portable Popup",
            TemplateKey: "template.view.portable",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "value",
                    "value",
                    Bindings:
                    [new EngineeringBindingDto(
                        "value",
                        EngineeringBindingKind.Tag,
                        "{equipmentPath}.Current",
                        "read")])
            ],
            Metadata: new Dictionary<string, string> { ["family"] = "popup" }));

        var libraryId = Guid.NewGuid();
        var sourcePackages = new ReusableLibraryPackageService(
            sourceAssets,
            sourceVisualAssets,
            sourceScripts,
            sourceViews);
        var libraryBytes = sourcePackages.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Portable Views",
            "1.0.0",
            [
                new(ReusableLibraryResourceKinds.Screen, screenId),
                new(ReusableLibraryResourceKinds.Popup, popupId)
            ]));
        var inspection = sourcePackages.Inspect(libraryBytes);

        Assert.Equal(4, inspection.Manifest.Resources.Count);
        var screenResource = Assert.Single(
            inspection.Manifest.Resources,
            resource => resource.Kind == ReusableLibraryResourceKinds.Screen && resource.ResourceId == screenId);
        var popupResource = Assert.Single(
            inspection.Manifest.Resources,
            resource => resource.Kind == ReusableLibraryResourceKinds.Popup && resource.ResourceId == popupId);
        Assert.Contains(screenResource.Dependencies, dependency =>
            dependency.Kind == ReusableLibraryResourceKinds.Dynamo && dependency.ResourceId == dynamoId);
        Assert.Contains(popupResource.Dependencies, dependency =>
            dependency.Kind == ReusableLibraryResourceKinds.EquipmentTemplate && dependency.ResourceId == templateId);

        using var target = CreateTarget();
        var screenPlan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Screen, screenId));
        Assert.True(screenPlan.RequiresMutation);
        Assert.Equal(2, screenPlan.DependencyClosure.Count);
        Assert.Single(screenPlan.Engineering.Screens!);
        Assert.Single(screenPlan.Engineering.Dynamos!);
        var screenApply = target.Exchange.Apply(screenPlan.Engineering, ImportMode.CreateOnly, screenPlan.ImportContext);
        Assert.DoesNotContain(screenApply.Issues, issue => issue.IsError);
        Assert.Equal(2, screenApply.Created);

        var popupPlan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Popup, popupId));
        Assert.True(popupPlan.RequiresMutation);
        Assert.Equal(2, popupPlan.DependencyClosure.Count);
        Assert.Single(popupPlan.Engineering.Popups!);
        Assert.Single(popupPlan.Engineering.Templates!);
        var popupApply = target.Exchange.Apply(popupPlan.Engineering, ImportMode.CreateOnly, popupPlan.ImportContext);
        Assert.DoesNotContain(popupApply.Issues, issue => issue.IsError);
        Assert.Equal(2, popupApply.Created);

        AssertOrigin(target.Views.FindScreen(screenId)!.Metadata, inspection.Manifest, screenResource);
        AssertOrigin(target.Views.FindPopup(popupId)!.Metadata, inspection.Manifest, popupResource);
        Assert.Equal("screen", target.Views.FindScreen(screenId)!.Metadata!["family"]);
        Assert.Equal("popup", target.Views.FindPopup(popupId)!.Metadata!["family"]);

        var screenAgain = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Screen, screenId));
        var popupAgain = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Popup, popupId));
        Assert.False(screenAgain.RequiresMutation);
        Assert.Equal(2, screenAgain.DeduplicatedCount);
        Assert.False(popupAgain.RequiresMutation);
        Assert.Equal(2, popupAgain.DeduplicatedCount);

        var projectPackages = new ProjectPackageService(target.Exchange, target.VisualAssets);
        var projectBytes = projectPackages.Export("portable-views-roundtrip", "Portable Views Roundtrip");

        using var restored = CreateTarget();
        var restoredPackages = new ProjectPackageService(restored.Exchange, restored.VisualAssets);
        var preview = restoredPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);
        var restoredApply = restoredPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(restoredApply.Issues, issue => issue.IsError);

        Assert.NotNull(restored.Views.FindScreen(screenId));
        Assert.NotNull(restored.Views.FindPopup(popupId));
        Assert.NotNull(restored.Assets.FindDynamo(dynamoId));
        Assert.NotNull(restored.Assets.FindTemplate(templateId));
        AssertOrigin(restored.Views.FindScreen(screenId)!.Metadata, inspection.Manifest, screenResource);
        AssertOrigin(restored.Views.FindPopup(popupId)!.Metadata, inspection.Manifest, popupResource);
    }

    [Fact]
    public void ScreenIncorporation_RejectsStableIdentityKeyCollision()
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceViews = new InMemoryEngineeringViewRegistry();
        var sourceScripts = new InMemoryScriptEngineeringRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var screenId = Guid.NewGuid();
        sourceViews.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "screen.collision",
            "Library Screen",
            Route: "/library"));
        var packages = new ReusableLibraryPackageService(
            sourceAssets,
            sourceVisualAssets,
            sourceScripts,
            sourceViews);
        var bytes = packages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Collision",
            "1",
            [new(ReusableLibraryResourceKinds.Screen, screenId)]));

        using var target = CreateTarget();
        target.Views.UpsertScreen(new ScreenEngineeringDto(
            Guid.NewGuid(),
            "screen.collision",
            "Existing Screen",
            Route: "/existing"));

        var exception = Assert.Throws<ReusableLibraryIncorporationConflictException>(() =>
            target.Incorporation.Plan(
                bytes,
                new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Screen, screenId)));
        Assert.Contains("stable ID/key collision", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOrigin(
        IReadOnlyDictionary<string, string>? metadata,
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource)
    {
        var actual = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(metadata);
        var payload = Assert.Single(manifest.Files, file => file.Path == resource.PayloadPath);
        Assert.Equal(manifest.LibraryId.ToString("D"), actual[ReusableLibraryProvenance.LibraryIdKey]);
        Assert.Equal(manifest.Version, actual[ReusableLibraryProvenance.LibraryVersionKey]);
        Assert.Equal(resource.ResourceId.ToString("D"), actual[ReusableLibraryProvenance.ResourceIdKey]);
        Assert.Equal(resource.Kind, actual[ReusableLibraryProvenance.ResourceKindKey]);
        Assert.Equal(payload.Sha256.ToLowerInvariant(), actual[ReusableLibraryProvenance.PayloadSha256Key]);
    }

    private static TargetHarness CreateTarget()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(eventBus);
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
        var packages = new ReusableLibraryPackageService(assets, visualAssets, scripts, views);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            assets,
            visualAssets,
            exchange,
            scripts,
            views);
        return new TargetHarness(alarms, assets, views, visualAssets, exchange, incorporation);
    }

    private sealed class TargetHarness(
        InMemoryAlarmEngine alarms,
        InMemoryEngineeringAssetRegistry assets,
        InMemoryEngineeringViewRegistry views,
        InMemoryVisualAssetEngineeringRegistry visualAssets,
        EngineeringExchangeService exchange,
        ReusableLibraryIncorporationService incorporation) : IDisposable
    {
        public InMemoryEngineeringAssetRegistry Assets { get; } = assets;
        public InMemoryEngineeringViewRegistry Views { get; } = views;
        public InMemoryVisualAssetEngineeringRegistry VisualAssets { get; } = visualAssets;
        public EngineeringExchangeService Exchange { get; } = exchange;
        public ReusableLibraryIncorporationService Incorporation { get; } = incorporation;

        public void Dispose() => alarms.Dispose();
    }
}
