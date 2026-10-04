using System.IO.Compression;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableDynamoDependencyAuthorityTests
{
    [Fact]
    public void Export_UsesSameCanonicalV1DependencyClosureAsAnalyzer()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.authority",
            "Authority Template"));

        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.authority",
            "Authority Asset",
            "authority.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256));

        var dynamo = new DynamoEngineeringDto(
            dynamoId,
            "dynamo.authority",
            "Authority Dynamo",
            TemplateKey: "template.authority",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "image",
                    "core.svgSymbol",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]);
        assets.UpsertDynamo(dynamo);

        var expected = ReusableDynamoDependencyAnalyzer
            .AnalyzeWorking(dynamo, assets, visualAssets)
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();

        var packages = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = packages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Authority Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Dynamo, dynamoId)]));
        var inspection = packages.Inspect(bytes);
        var exported = Assert.Single(
            inspection.Manifest.Resources,
            resource => resource.Kind == ReusableLibraryResourceKinds.Dynamo && resource.ResourceId == dynamoId);
        var actual = exported.Dependencies
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();

        Assert.Equal(expected, actual);
        Assert.Contains((ReusableLibraryResourceKinds.EquipmentTemplate, templateId), actual);
        Assert.Contains((ReusableLibraryResourceKinds.VisualAsset, assetId), actual);
    }

    [Fact]
    public void AnalyzeWorking_DeduplicatesRepeatedSvgAssetDependency()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.shared",
            "Shared SVG",
            "shared.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256));

        var assetRef = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" });
        var dynamo = new DynamoEngineeringDto(
            Guid.NewGuid(),
            "dynamo.shared-svg",
            "Shared SVG Dynamo",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "symbol-a",
                    "core.svgSymbol",
                    Properties: new Dictionary<string, JsonElement> { ["assetRef"] = assetRef }),
                new VisualElementEngineeringDto(
                    "symbol-b",
                    "core.svgSymbol",
                    Properties: new Dictionary<string, JsonElement> { ["assetRef"] = assetRef })
            ]);

        var dependencies = ReusableDynamoDependencyAnalyzer.AnalyzeWorking(dynamo, assets, visualAssets);

        var visualAsset = Assert.Single(
            dependencies,
            dependency => dependency.Kind == ReusableLibraryResourceKinds.VisualAsset);
        Assert.Equal(assetId, visualAsset.ResourceId);
    }

    [Fact]
    public void AnalyzeWorking_RejectsMissingSvgAsset()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var missingId = Guid.NewGuid();
        var dynamo = new DynamoEngineeringDto(
            Guid.NewGuid(),
            "dynamo.missing-svg",
            "Missing SVG Dynamo",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "symbol",
                    "core.svgSymbol",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{missingId:D}" })
                    })
            ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReusableDynamoDependencyAnalyzer.AnalyzeWorking(dynamo, assets, visualAssets));

        Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeWorking_RejectsNonSvgAssetForSvgSymbol()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/png", [0x89, 0x50, 0x4E, 0x47]);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.not-svg",
            "Not SVG",
            "not-svg.png",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256));

        var dynamo = new DynamoEngineeringDto(
            Guid.NewGuid(),
            "dynamo.non-svg",
            "Non SVG Dynamo",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "symbol",
                    "core.svgSymbol",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => ReusableDynamoDependencyAnalyzer.AnalyzeWorking(dynamo, assets, visualAssets));

        Assert.Contains("not image/svg+xml", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_AllowsPortableCommandParametersWithoutCapturingProjectCommandIdentity()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var dynamoId = Guid.NewGuid();
        var forbiddenProjectCommandId = Guid.NewGuid();
        var dynamo = new DynamoEngineeringDto(
            dynamoId,
            "dynamo.portable-command",
            "Portable Command Dynamo",
            Parameters:
            [
                new DynamoParameterDefinitionEngineeringDto(
                    "startCommand",
                    DynamoParameterKind.Command,
                    Required: true),
                new DynamoParameterDefinitionEngineeringDto(
                    "stopCommand",
                    DynamoParameterKind.Command,
                    Required: true)
            ],
            Elements:
            [
                new VisualElementEngineeringDto(
                    "start",
                    "core.button",
                    Actions:
                    [
                        new VisualNavigationActionEngineeringDto(
                            "click",
                            VisualNavigationActionKind.ExecuteCommand,
                            CommandParameterKey: "startCommand")
                    ]),
                new VisualElementEngineeringDto(
                    "stop",
                    "core.button",
                    Actions:
                    [
                        new VisualNavigationActionEngineeringDto(
                            "click",
                            VisualNavigationActionKind.ExecuteCommand,
                            CommandParameterKey: "stopCommand")
                    ])
            ]);
        assets.UpsertDynamo(dynamo);

        var packages = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = packages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Portable Command Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Dynamo, dynamoId)]));
        var inspection = packages.Inspect(bytes);
        var resource = Assert.Single(
            inspection.Manifest.Resources,
            candidate => candidate.Kind == ReusableLibraryResourceKinds.Dynamo &&
                candidate.ResourceId == dynamoId);

        Assert.Empty(resource.Dependencies);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var payload = archive.GetEntry(resource.PayloadPath);
        Assert.NotNull(payload);
        using var reader = new StreamReader(payload!.Open());
        var json = reader.ReadToEnd();

        Assert.Contains("startCommand", json, StringComparison.Ordinal);
        Assert.Contains("stopCommand", json, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenProjectCommandId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeWorking_RejectsConcreteCommandIdentityInsideReusableDynamo()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var commandId = Guid.NewGuid();
        var dynamo = new DynamoEngineeringDto(
            Guid.NewGuid(),
            "dynamo.bound-command",
            "Bound Command Dynamo",
            Parameters:
            [
                new DynamoParameterDefinitionEngineeringDto(
                    "startCommand",
                    DynamoParameterKind.Command,
                    Required: true)
            ],
            Elements:
            [
                new VisualElementEngineeringDto(
                    "start",
                    "core.button",
                    Actions:
                    [
                        new VisualNavigationActionEngineeringDto(
                            "click",
                            VisualNavigationActionKind.ExecuteCommand,
                            CommandId: commandId)
                    ])
            ]);

        var error = Assert.Throws<InvalidDataException>(
            () => ReusableDynamoDependencyAnalyzer.AnalyzeWorking(dynamo, assets, visualAssets));

        Assert.Contains("non-portable action", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_RejectsNestedDynamoThroughCanonicalV1Analyzer()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var childId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        assets.UpsertDynamo(new DynamoEngineeringDto(
            childId,
            "dynamo.child",
            "Child Dynamo"));
        assets.UpsertDynamo(new DynamoEngineeringDto(
            rootId,
            "dynamo.root",
            "Root Dynamo",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "child",
                    "dynamo",
                    DynamoKey: "dynamo.child")
            ]));

        var packages = new ReusableLibraryPackageService(assets, visualAssets);

        var exception = Assert.Throws<InvalidDataException>(() => packages.Export(
            new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Nested Dynamo Library",
                "1.0.0",
                [new(ReusableLibraryResourceKinds.Dynamo, rootId)])));

        Assert.Contains("does not support nested Dynamos", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
