using System.Buffers.Binary;
using System.Text.Json;
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

public sealed class ReusableLibraryProvenanceTests
{
    [Fact]
    public void Incorporation_StampsDeterministicOrigin_RededupeIsIdempotent_AndProjectPackagePreservesIt()
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var libraryId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        sourceAssets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.provenance",
            "Provenance Template",
            Metadata: new Dictionary<string, string>
            {
                ["owner"] = "engineering",
                [ReusableLibraryProvenance.LibraryIdKey] = Guid.NewGuid().ToString("D")
            }));

        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        sourceVisualAssets.PutPayload(payload);
        sourceVisualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.provenance",
            "Provenance Asset",
            "provenance.bmp",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            1,
            1,
            Metadata: new Dictionary<string, string> { ["family"] = "symbol" }));

        sourceAssets.UpsertDynamo(new DynamoEngineeringDto(
            dynamoId,
            "dynamo.provenance",
            "Provenance Dynamo",
            TemplateKey: "template.provenance",
            Metadata: new Dictionary<string, string> { ["class"] = "pump" },
            Elements:
            [
                new VisualElementEngineeringDto(
                    "image",
                    "core.image",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]));

        var libraryPackages = new ReusableLibraryPackageService(sourceAssets, sourceVisualAssets);
        var libraryBytes = libraryPackages.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Provenance Library",
            "2.4.0",
            [new(ReusableLibraryResourceKinds.Dynamo, dynamoId)]));
        var libraryInspection = libraryPackages.Inspect(libraryBytes);

        using var working = CreateTarget();
        var firstPlan = working.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Dynamo, dynamoId));

        Assert.True(firstPlan.RequiresMutation);
        Assert.Equal(3, firstPlan.DependencyClosure.Count);
        Assert.Single(firstPlan.Engineering.Templates!);
        Assert.Single(firstPlan.Engineering.Dynamos!);
        Assert.Single(firstPlan.Engineering.VisualAssets!);

        AssertOrigin(
            firstPlan.Engineering.Templates!.Single(),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId);
        AssertOrigin(
            firstPlan.Engineering.Dynamos!.Single(),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.Dynamo,
            dynamoId);
        AssertOrigin(
            firstPlan.Engineering.VisualAssets!.Single(),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.VisualAsset,
            assetId);

        Assert.Equal("engineering", firstPlan.Engineering.Templates!.Single().Metadata!["owner"]);
        Assert.Equal("pump", firstPlan.Engineering.Dynamos!.Single().Metadata!["class"]);
        Assert.Equal("symbol", firstPlan.Engineering.VisualAssets!.Single().Metadata!["family"]);

        var firstResult = working.Exchange.Apply(
            firstPlan.Engineering,
            ImportMode.CreateOnly,
            firstPlan.ImportContext);
        Assert.DoesNotContain(firstResult.Issues, issue => issue.IsError);
        Assert.Equal(3, firstResult.Created);

        var secondPlan = working.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Dynamo, dynamoId));

        Assert.False(secondPlan.RequiresMutation);
        Assert.Equal(3, secondPlan.DeduplicatedCount);
        Assert.Equal(0, secondPlan.Preview.CreateCount);

        var projectPackages = new ProjectPackageService(working.Exchange, working.VisualAssets);
        var projectBytes = projectPackages.Export("provenance-roundtrip", "Provenance Roundtrip");

        // Restore with no .escadalib and no reusable-library catalog present.
        using var restored = CreateTarget();
        var restoredPackages = new ProjectPackageService(restored.Exchange, restored.VisualAssets);
        var preview = restoredPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);
        var result = restoredPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        AssertOrigin(
            Assert.IsType<EquipmentTemplateEngineeringDto>(restored.Assets.FindTemplate(templateId)),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.EquipmentTemplate,
            templateId);
        AssertOrigin(
            Assert.IsType<DynamoEngineeringDto>(restored.Assets.FindDynamo(dynamoId)),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.Dynamo,
            dynamoId);
        AssertOrigin(
            Assert.IsType<VisualAssetEngineeringDto>(restored.VisualAssets.FindAsset(assetId)),
            libraryInspection.Manifest,
            ReusableLibraryResourceKinds.VisualAsset,
            assetId);
        Assert.True(restored.VisualAssets.FindPayload(payload.Sha256)!.Content.AsSpan().SequenceEqual(payload.Content));
    }

    private static void AssertOrigin(
        EquipmentTemplateEngineeringDto value,
        ReusableLibraryManifest manifest,
        string kind,
        Guid resourceId) =>
        AssertOrigin(value.Metadata, manifest, kind, resourceId);

    private static void AssertOrigin(
        DynamoEngineeringDto value,
        ReusableLibraryManifest manifest,
        string kind,
        Guid resourceId) =>
        AssertOrigin(value.Metadata, manifest, kind, resourceId);

    private static void AssertOrigin(
        VisualAssetEngineeringDto value,
        ReusableLibraryManifest manifest,
        string kind,
        Guid resourceId) =>
        AssertOrigin(value.Metadata, manifest, kind, resourceId);

    private static void AssertOrigin(
        IReadOnlyDictionary<string, string>? metadata,
        ReusableLibraryManifest manifest,
        string kind,
        Guid resourceId)
    {
        var actual = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(metadata);
        var resource = Assert.Single(
            manifest.Resources,
            entry => entry.Kind == kind && entry.ResourceId == resourceId);
        var payload = Assert.Single(manifest.Files, file => file.Path == resource.PayloadPath);

        Assert.Equal(manifest.LibraryId.ToString("D"), actual[ReusableLibraryProvenance.LibraryIdKey]);
        Assert.Equal(manifest.Version, actual[ReusableLibraryProvenance.LibraryVersionKey]);
        Assert.Equal(resourceId.ToString("D"), actual[ReusableLibraryProvenance.ResourceIdKey]);
        Assert.Equal(kind, actual[ReusableLibraryProvenance.ResourceKindKey]);
        Assert.Equal(payload.Sha256.ToLowerInvariant(), actual[ReusableLibraryProvenance.PayloadSha256Key]);
        Assert.DoesNotContain(actual.Keys, key => key.Contains("path", StringComparison.OrdinalIgnoreCase));
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
        var packages = new ReusableLibraryPackageService(assets, visualAssets);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            assets,
            visualAssets,
            exchange);
        return new TargetHarness(alarms, assets, visualAssets, exchange, incorporation);
    }

    private static byte[] CreateBmp()
    {
        const int fileSize = 58;
        var bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34, 4), 4);
        return bytes;
    }

    private sealed class TargetHarness(
        InMemoryAlarmEngine alarms,
        InMemoryEngineeringAssetRegistry assets,
        InMemoryVisualAssetEngineeringRegistry visualAssets,
        EngineeringExchangeService exchange,
        ReusableLibraryIncorporationService incorporation) : IDisposable
    {
        public InMemoryEngineeringAssetRegistry Assets { get; } = assets;
        public InMemoryVisualAssetEngineeringRegistry VisualAssets { get; } = visualAssets;
        public EngineeringExchangeService Exchange { get; } = exchange;
        public ReusableLibraryIncorporationService Incorporation { get; } = incorporation;

        public void Dispose() => alarms.Dispose();
    }
}
