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
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryIncorporationServiceTests
{
    [Fact]
    public void Plan_NewTemplate_ProducesCanonicalCreateWithoutMutatingTarget()
    {
        var templateId = Guid.NewGuid();
        var template = new EquipmentTemplateEngineeringDto(
            templateId,
            "library.template",
            "Library Template",
            Properties: new Dictionary<string, string> { ["family"] = "pump" });
        var library = CreateTemplateLibrary(template);
        using var target = CreateTarget();
        var baseline = target.Assets.SnapshotTemplates().Count;

        var plan = target.Incorporation.Plan(
            library.Bytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));

        Assert.True(plan.RequiresMutation);
        Assert.Equal(1, plan.Preview.CreateCount);
        Assert.Equal(0, plan.DeduplicatedCount);
        Assert.Single(plan.DependencyClosure);
        Assert.Single(plan.Engineering.Templates!);
        Assert.Equal(baseline, target.Assets.SnapshotTemplates().Count);
        Assert.Null(target.Assets.FindTemplate(templateId));

        var result = target.Exchange.Apply(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Created);
        var incorporated = Assert.IsType<EquipmentTemplateEngineeringDto>(target.Assets.FindTemplate(templateId));
        Assert.Equal(templateId, incorporated.Id);
        Assert.Equal("library.template", incorporated.Key);
        Assert.Equal("Library Template", incorporated.Name);
        Assert.Equal("pump", incorporated.Properties!["family"]);
    }

    [Fact]
    public void Plan_IdenticalTemplateAlreadyInProject_DeduplicatesWithoutMutation()
    {
        var templateId = Guid.NewGuid();
        var template = new EquipmentTemplateEngineeringDto(
            templateId,
            "library.same",
            "Same Template",
            Context: new Dictionary<string, string> { ["area"] = "generic" });
        var library = CreateTemplateLibrary(template);
        using var target = CreateTarget();
        target.Assets.UpsertTemplate(template);
        var before = target.Assets.SnapshotTemplates().Count;

        var plan = target.Incorporation.Plan(
            library.Bytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));

        Assert.False(plan.RequiresMutation);
        Assert.Equal(1, plan.DeduplicatedCount);
        Assert.Equal(0, plan.Preview.CreateCount);
        Assert.Empty(plan.Engineering.Templates!);
        Assert.Equal(before, target.Assets.SnapshotTemplates().Count);
    }

    [Fact]
    public void Plan_TemplateKeyCollisionWithDifferentIdentity_FailsBeforeMutation()
    {
        var templateId = Guid.NewGuid();
        var library = CreateTemplateLibrary(new EquipmentTemplateEngineeringDto(
            templateId,
            "collision.template",
            "Library Template"));
        using var target = CreateTarget();
        var existingId = Guid.NewGuid();
        target.Assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            existingId,
            "collision.template",
            "Existing Project Template"));

        var exception = Assert.Throws<ReusableLibraryIncorporationConflictException>(() =>
            target.Incorporation.Plan(
                library.Bytes,
                new ReusableLibraryIncorporationSelection(
                    ReusableLibraryResourceKinds.EquipmentTemplate,
                    templateId)));

        Assert.Equal(templateId, exception.ResourceId);
        Assert.Equal("stable ID/key collision", exception.Reason);
        Assert.Equal(existingId, target.Assets.FindTemplateByKey("collision.template")!.Id);
        Assert.Null(target.Assets.FindTemplate(templateId));
    }

    [Fact]
    public void Plan_SameTemplateIdentityWithDifferentContent_FailsBeforeMutation()
    {
        var templateId = Guid.NewGuid();
        var library = CreateTemplateLibrary(new EquipmentTemplateEngineeringDto(
            templateId,
            "drift.template",
            "Library Version"));
        using var target = CreateTarget();
        target.Assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "drift.template",
            "Project Version"));

        var exception = Assert.Throws<ReusableLibraryIncorporationConflictException>(() =>
            target.Incorporation.Plan(
                library.Bytes,
                new ReusableLibraryIncorporationSelection(
                    ReusableLibraryResourceKinds.EquipmentTemplate,
                    templateId)));

        Assert.Equal("same identity has different canonical content", exception.Reason);
        Assert.Equal("Project Version", target.Assets.FindTemplate(templateId)!.Name);
    }

    [Fact]
    public void Plan_NewVisualAsset_CarriesVerifiedSidecarThroughCanonicalImportContext()
    {
        var assetId = Guid.NewGuid();
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisual = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        sourceVisual.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "library.image",
            "Library Image",
            "image.bmp",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            1,
            1);
        sourceVisual.UpsertAsset(asset);
        var package = new ReusableLibraryPackageService(sourceAssets, sourceVisual);
        var bytes = package.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Visual Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.VisualAsset, assetId)]));
        using var target = CreateTarget();

        var plan = target.Incorporation.Plan(
            bytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.VisualAsset,
                assetId));

        Assert.True(plan.RequiresMutation);
        Assert.Single(plan.Engineering.VisualAssets!);
        Assert.True(plan.ImportContext.TryGetVisualAssetPayload(payload.Sha256, out var plannedPayload));
        Assert.True(plannedPayload.Content.AsSpan().SequenceEqual(payload.Content));
        Assert.Null(target.VisualAssets.FindAsset(assetId));

        var result = target.Exchange.Apply(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Created);
        var incorporated = Assert.IsType<VisualAssetEngineeringDto>(target.VisualAssets.FindAsset(assetId));
        Assert.Equal(assetId, incorporated.Id);
        Assert.Equal("library.image", incorporated.Key);
        Assert.Equal(payload.Sha256, incorporated.Sha256);
        Assert.True(target.VisualAssets.FindPayload(payload.Sha256)!.Content.AsSpan().SequenceEqual(payload.Content));
    }

    [Fact]
    public void Plan_IdenticalVisualAssetAlreadyInProject_DeduplicatesVerifiedPayload()
    {
        var assetId = Guid.NewGuid();
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisual = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        sourceVisual.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "library.raster",
            "Library Raster",
            "raster.bmp",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            1,
            1);
        sourceVisual.UpsertAsset(asset);
        var package = new ReusableLibraryPackageService(sourceAssets, sourceVisual);
        var bytes = package.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Raster Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.VisualAsset, assetId)]));
        using var target = CreateTarget();
        target.VisualAssets.PutPayload(payload);
        target.VisualAssets.UpsertAsset(asset);

        var plan = target.Incorporation.Plan(
            bytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.VisualAsset,
                assetId));

        Assert.False(plan.RequiresMutation);
        Assert.Equal(1, plan.DeduplicatedCount);
        Assert.Empty(plan.Engineering.VisualAssets!);
        Assert.True(target.VisualAssets.FindPayload(payload.Sha256)!.Content.AsSpan().SequenceEqual(payload.Content));
    }

    [Fact]
    public void Plan_UnknownSelectedResource_FailsWithoutProjectMutation()
    {
        var templateId = Guid.NewGuid();
        var library = CreateTemplateLibrary(new EquipmentTemplateEngineeringDto(
            templateId,
            "known.template",
            "Known Template"));
        using var target = CreateTarget();

        Assert.Throws<KeyNotFoundException>(() =>
            target.Incorporation.Plan(
                library.Bytes,
                new ReusableLibraryIncorporationSelection(
                    ReusableLibraryResourceKinds.EquipmentTemplate,
                    Guid.NewGuid())));
        Assert.Empty(target.Assets.SnapshotTemplates());
    }

    private static (byte[] Bytes, ReusableLibraryInspection Inspection) CreateTemplateLibrary(
        EquipmentTemplateEngineeringDto template)
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisual = new InMemoryVisualAssetEngineeringRegistry();
        sourceAssets.UpsertTemplate(template);
        var package = new ReusableLibraryPackageService(sourceAssets, sourceVisual);
        var bytes = package.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Template Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.EquipmentTemplate, template.Id!.Value)]));
        return (bytes, package.Inspect(bytes));
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
        var package = new ReusableLibraryPackageService(assets, visualAssets);
        var incorporation = new ReusableLibraryIncorporationService(
            package,
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
