using System.Buffers.Binary;
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

public sealed class ReusableLibraryProjectPackageRoundtripTests
{
    [Fact]
    public void IncorporatedTemplateAndRaster_RoundTripThroughProjectPackageWithoutLibraryDependency()
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        sourceAssets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "library.roundtrip.template",
            "Roundtrip Template",
            Properties: new Dictionary<string, string> { ["family"] = "pump" }));

        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        sourceVisualAssets.PutPayload(payload);
        sourceVisualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "library.roundtrip.asset",
            "Roundtrip Asset",
            "roundtrip.bmp",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            1,
            1));

        var libraryPackages = new ReusableLibraryPackageService(sourceAssets, sourceVisualAssets);
        var libraryBytes = libraryPackages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Roundtrip Library",
            "1.0.0",
            [
                new(ReusableLibraryResourceKinds.EquipmentTemplate, templateId),
                new(ReusableLibraryResourceKinds.VisualAsset, assetId)
            ]));

        using var working = CreateTarget();
        Incorporate(working, libraryBytes, ReusableLibraryResourceKinds.EquipmentTemplate, templateId);
        Incorporate(working, libraryBytes, ReusableLibraryResourceKinds.VisualAsset, assetId);

        var projectPackages = new ProjectPackageService(working.Exchange, working.VisualAssets);
        var projectBytes = projectPackages.Export("library-roundtrip", "Library Roundtrip");
        var inspection = projectPackages.Inspect(projectBytes);

        Assert.Contains(inspection.Engineering.Templates ?? [], template => template.Id == templateId);
        Assert.Contains(inspection.Engineering.VisualAssets ?? [], asset => asset.Id == assetId);
        Assert.Contains(
            inspection.Manifest.Files,
            file => file.Path == $"assets/{payload.Sha256}" && file.Sha256 == payload.Sha256);

        // No reusable-library catalog or .escadalib participates in this restore.
        using var restored = CreateTarget();
        var restoredPackages = new ProjectPackageService(restored.Exchange, restored.VisualAssets);
        var preview = restoredPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);

        var result = restoredPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var restoredTemplate = Assert.IsType<EquipmentTemplateEngineeringDto>(
            restored.Assets.FindTemplate(templateId));
        Assert.Equal("library.roundtrip.template", restoredTemplate.Key);
        Assert.Equal("pump", restoredTemplate.Properties!["family"]);

        var restoredAsset = Assert.IsType<VisualAssetEngineeringDto>(
            restored.VisualAssets.FindAsset(assetId));
        Assert.Equal("library.roundtrip.asset", restoredAsset.Key);
        Assert.Equal(payload.Sha256, restoredAsset.Sha256);
        var restoredPayload = Assert.IsType<VisualAssetPayload>(
            restored.VisualAssets.FindPayload(payload.Sha256));
        Assert.True(restoredPayload.Content.AsSpan().SequenceEqual(payload.Content));
    }

    private static void Incorporate(
        TargetHarness target,
        byte[] libraryBytes,
        string kind,
        Guid resourceId)
    {
        var plan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(kind, resourceId));
        Assert.True(plan.RequiresMutation);
        var result = target.Exchange.Apply(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.True(result.Created > 0);
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
        var libraryPackages = new ReusableLibraryPackageService(assets, visualAssets);
        var incorporation = new ReusableLibraryIncorporationService(
            libraryPackages,
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
