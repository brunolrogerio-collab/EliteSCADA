using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableViewDependencyAnalyzerTests
{
    [Fact]
    public void Screen_ResolvesDynamoAndVisualAssetClosure()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var dynamoId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        assets.UpsertDynamo(new DynamoEngineeringDto(dynamoId, "dynamo.portable", "Portable Dynamo"));
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.portable",
            "Portable Asset",
            "portable.bmp",
            "image/bmp",
            58,
            new string('a', 64),
            1,
            1));

        var screen = new ScreenEngineeringDto(
            Guid.NewGuid(),
            "screen.portable",
            "Portable Screen",
            Elements:
            [
                new VisualElementEngineeringDto("dynamo", "dynamo", DynamoKey: "dynamo.portable"),
                new VisualElementEngineeringDto(
                    "image",
                    "core.image",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]);

        var dependencies = ReusableViewDependencyAnalyzer.AnalyzeWorking(screen, assets, visualAssets);

        Assert.Equal(2, dependencies.Count);
        Assert.Contains(dependencies, item => item.Kind == ReusableLibraryResourceKinds.Dynamo && item.ResourceId == dynamoId);
        Assert.Contains(dependencies, item => item.Kind == ReusableLibraryResourceKinds.VisualAsset && item.ResourceId == assetId);
    }

    [Fact]
    public void Popup_ResolvesTemplateAndAllowsParameterizedBindings()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.portable",
            "Portable Template"));

        var popup = new PopupEngineeringDto(
            Guid.NewGuid(),
            "popup.portable",
            "Portable Popup",
            TemplateKey: "template.portable",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "value",
                    "value",
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            "value",
                            EngineeringBindingKind.Tag,
                            "{equipmentPath}.Current",
                            "read")
                    ])
            ]);

        var dependencies = ReusableViewDependencyAnalyzer.AnalyzeWorking(popup, assets, visualAssets);

        var dependency = Assert.Single(dependencies);
        Assert.Equal(ReusableLibraryResourceKinds.EquipmentTemplate, dependency.Kind);
        Assert.Equal(templateId, dependency.ResourceId);
    }

    [Fact]
    public void Screen_RejectsConcreteProjectDataAndActions()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();

        var concreteData = new ScreenEngineeringDto(
            Guid.NewGuid(),
            "screen.data-bound",
            "Data Bound",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "value",
                    "value",
                    Bindings:
                    [new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Plant.P01.Current", "read")])
            ]);
        var dataException = Assert.Throws<InvalidDataException>(() =>
            ReusableViewDependencyAnalyzer.AnalyzeWorking(concreteData, assets, visualAssets));
        Assert.Contains("concrete project data", dataException.Message, StringComparison.OrdinalIgnoreCase);

        var actionBound = new ScreenEngineeringDto(
            Guid.NewGuid(),
            "screen.action-bound",
            "Action Bound",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "button",
                    "button",
                    Actions:
                    [new VisualNavigationActionEngineeringDto(
                        "click",
                        VisualNavigationActionKind.NavigateScreen,
                        TargetKey: "screen.other")])
            ]);
        var actionException = Assert.Throws<InvalidDataException>(() =>
            ReusableViewDependencyAnalyzer.AnalyzeWorking(actionBound, assets, visualAssets));
        Assert.Contains("navigation/command actions", actionException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Screen_RejectsProjectScriptHmiCoupling()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var scripts = new InMemoryScriptEngineeringRegistry();
        var screenId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        scripts.Upsert(new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/view",
            "View Script",
            ScriptEngineeringScope.ClientVisual,
            "def run():\n    pass",
            entryPoints: [new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "run")]));
        scripts.ReplaceVisualEventReferences(
            scriptId,
            [new ScriptVisualEventReference(
                screenId,
                null,
                ScriptEngineeringEventKind.Initialize,
                scriptId,
                "run")]);

        var screen = new ScreenEngineeringDto(screenId, "screen.script-bound", "Script Bound");
        var exception = Assert.Throws<InvalidDataException>(() =>
            ReusableViewDependencyAnalyzer.AnalyzeWorking(screen, assets, visualAssets, scripts));

        Assert.Contains("Script event associations", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestValidation_RequiresExactDerivedDependencySet()
    {
        var screenId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var screen = new ScreenEngineeringDto(
            screenId,
            "screen.manifest",
            "Manifest Screen",
            Elements: [new VisualElementEngineeringDto("dynamo", "dynamo", DynamoKey: "dynamo.manifest")]);
        var screenResource = new ReusableLibraryResourceEntry(
            screenId,
            ReusableLibraryResourceKinds.Screen,
            screen.Key,
            screen.Name,
            $"resources/screen/{screenId:D}.json",
            Array.Empty<ReusableLibraryDependency>());
        var dynamoResource = new ReusableLibraryResourceEntry(
            dynamoId,
            ReusableLibraryResourceKinds.Dynamo,
            "dynamo.manifest",
            "Manifest Dynamo",
            $"resources/dynamo/{dynamoId:D}.json",
            Array.Empty<ReusableLibraryDependency>());
        var manifest = new ReusableLibraryManifest(
            ReusableLibraryPackageService.CurrentFormat,
            ReusableLibraryPackageService.CurrentFormatVersion,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "EliteSCADA",
            "Manifest",
            "1",
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            [screenResource, dynamoResource],
            Array.Empty<ReusableLibraryFileEntry>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            ReusableViewDependencyAnalyzer.ValidateDeclaredDependencies(screen, screenResource, manifest));
        Assert.Contains("do not exactly match", exception.Message, StringComparison.OrdinalIgnoreCase);

        var corrected = screenResource with
        {
            Dependencies = [new ReusableLibraryDependency(ReusableLibraryResourceKinds.Dynamo, dynamoId)]
        };
        ReusableViewDependencyAnalyzer.ValidateDeclaredDependencies(screen, corrected, manifest);
    }
}
