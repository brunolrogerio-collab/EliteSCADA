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
                    "core.image",
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
