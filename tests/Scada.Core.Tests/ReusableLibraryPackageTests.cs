using System.IO.Compression;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryPackageTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [Fact]
    public void ExportAndInspect_RoundTripsTemplateAndVisualAssetWithoutMutatingWorkingRegistries()
    {
        var changes = 0;
        var assets = new InMemoryEngineeringAssetRegistry(() => changes++);
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry(() => changes++);
        var templateId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "pump.template",
            "Pump Template",
            Properties: new Dictionary<string, string> { ["family"] = "pump" }));

        var payload = VisualAssetPayload.Create("image/png", new byte[] { 1, 2, 3, 4, 5, 6 });
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.pump",
            "Pump image",
            "pump.png",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            16,
            16));

        var baselineChanges = changes;
        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var libraryId = Guid.NewGuid();
        var bytes = service.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Pump Library",
            "1.0.0",
            [
                new(ReusableLibraryResourceKinds.EquipmentTemplate, templateId),
                new(ReusableLibraryResourceKinds.VisualAsset, assetId)
            ]));

        Assert.Equal(baselineChanges, changes);

        var inspection = service.Inspect(bytes);

        Assert.Equal(baselineChanges, changes);
        Assert.Equal(ReusableLibraryPackageService.CurrentFormat, inspection.Manifest.Format);
        Assert.Equal(ReusableLibraryPackageService.CurrentFormatVersion, inspection.Manifest.FormatVersion);
        Assert.Equal(libraryId, inspection.Manifest.LibraryId);
        Assert.Equal("Pump Library", inspection.Manifest.Name);
        Assert.Equal("1.0.0", inspection.Manifest.Version);
        Assert.Equal(EngineeringExchangeService.CurrentSchema, inspection.Manifest.EngineeringSchema);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspection.Manifest.EngineeringSchemaVersion);
        Assert.Equal(2, inspection.Manifest.Resources.Count);
        Assert.Equal(3, inspection.Manifest.Files.Count);
        Assert.Contains(inspection.Manifest.Resources, x =>
            x.ResourceId == templateId && x.Kind == ReusableLibraryResourceKinds.EquipmentTemplate);
        Assert.Contains(inspection.Manifest.Resources, x =>
            x.ResourceId == assetId && x.Kind == ReusableLibraryResourceKinds.VisualAsset);
        Assert.Contains(inspection.Manifest.Files, x =>
            x.Path == $"assets/{payload.Sha256}" &&
            x.Sha256 == payload.Sha256 &&
            x.Length == payload.ByteLength &&
            x.MediaType == payload.MediaType);
    }

    [Fact]
    public void Inspect_RejectsTamperedCanonicalResourcePayload()
    {
        var (service, bytes) = CreateTemplateLibrary();
        var tampered = RewriteArchive(bytes, (path, content) =>
        {
            if (!path.StartsWith("resources/", StringComparison.Ordinal)) return content;
            var copy = content.ToArray();
            copy[^1] ^= 0x01;
            return copy;
        });

        var exception = Assert.Throws<InvalidDataException>(() => service.Inspect(tampered));

        Assert.Contains("SHA-256", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_RejectsTamperedVisualAssetSidecar()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.logo",
            "Logo",
            "logo.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256));
        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Assets",
            "1",
            [new(ReusableLibraryResourceKinds.VisualAsset, assetId)]));

        var tampered = RewriteArchive(bytes, (path, content) =>
        {
            if (!path.StartsWith("assets/", StringComparison.Ordinal)) return content;
            var copy = content.ToArray();
            copy[0] ^= 0x01;
            return copy;
        });

        Assert.Throws<InvalidDataException>(() => service.Inspect(tampered));
    }

    [Fact]
    public void Inspect_RejectsTraversalArchiveMemberBeforeManifestResolution()
    {
        var (service, bytes) = CreateTemplateLibrary();
        var malicious = RewriteArchive(bytes, static (_, content) => content, "../escape.txt", new byte[] { 1 });

        var exception = Assert.Throws<InvalidDataException>(() => service.Inspect(malicious));

        Assert.Contains("archive path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_RejectsUnsupportedEngineeringSchemaVersion()
    {
        var (service, bytes) = CreateTemplateLibrary();
        var modified = RewriteManifest(bytes, manifest => manifest with
        {
            EngineeringSchemaVersion = EngineeringExchangeService.CurrentSchemaVersion + 1
        });

        var exception = Assert.Throws<InvalidDataException>(() => service.Inspect(modified));

        Assert.Contains("schema version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_RejectsDuplicateStableSelection()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var id = Guid.NewGuid();
        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(id, "template.a", "Template A"));
        var service = new ReusableLibraryPackageService(assets, visualAssets);

        var exception = Assert.Throws<InvalidDataException>(() => service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Library",
            "1",
            [
                new(ReusableLibraryResourceKinds.EquipmentTemplate, id),
                new(ReusableLibraryResourceKinds.EquipmentTemplate, id)
            ])));

        Assert.Contains("more than once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_DynamoAutomaticallyIncludesV1ReusableDependencyClosure()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.pump",
            "Pump Template"));

        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.symbol",
            "Symbol",
            "symbol.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256));

        assets.UpsertDynamo(new DynamoEngineeringDto(
            rootId,
            "dynamo.root",
            "Root Dynamo",
            TemplateKey: "template.pump",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "symbol",
                    "core.image",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]));

        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Dynamo Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Dynamo, rootId)]));

        var inspection = service.Inspect(bytes);
        var root = Assert.Single(inspection.Manifest.Resources, resource => resource.ResourceId == rootId);

        Assert.Equal(3, inspection.Manifest.Resources.Count);
        Assert.Equal(4, inspection.Manifest.Files.Count);
        Assert.Equal(2, root.Dependencies.Count);
        Assert.Contains(root.Dependencies, dependency =>
            dependency.Kind == ReusableLibraryResourceKinds.EquipmentTemplate && dependency.ResourceId == templateId);
        Assert.Contains(root.Dependencies, dependency =>
            dependency.Kind == ReusableLibraryResourceKinds.VisualAsset && dependency.ResourceId == assetId);
        Assert.Contains(inspection.Manifest.Resources, resource =>
            resource.Kind == ReusableLibraryResourceKinds.EquipmentTemplate && resource.ResourceId == templateId);
        Assert.Contains(inspection.Manifest.Resources, resource =>
            resource.Kind == ReusableLibraryResourceKinds.VisualAsset && resource.ResourceId == assetId);
    }

    [Fact]
    public void Analyzer_DynamoV1RejectsNestedDynamoInsteadOfExpandingProductContract()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var childId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        assets.UpsertDynamo(new DynamoEngineeringDto(childId, "dynamo.child", "Child"));
        var root = new DynamoEngineeringDto(
            rootId,
            "dynamo.root",
            "Root",
            Elements: [new VisualElementEngineeringDto("child", "dynamo", DynamoKey: "dynamo.child")]);
        assets.UpsertDynamo(root);

        var exception = Assert.Throws<InvalidDataException>(() =>
            ReusableDynamoDependencyAnalyzer.AnalyzeWorking(root, assets, visualAssets));

        Assert.Contains("does not support nested Dynamos", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_DynamoRejectsConcreteProjectBindingBeforePackaging()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var id = Guid.NewGuid();
        assets.UpsertDynamo(new DynamoEngineeringDto(
            id,
            "dynamo.bound",
            "Project Bound Dynamo",
            Bindings:
            [
                new EngineeringBindingDto(
                    "value",
                    EngineeringBindingKind.Tag,
                    "Plant.P01.Speed")
            ]));
        var service = new ReusableLibraryPackageService(assets, visualAssets);

        var exception = Assert.Throws<InvalidDataException>(() => service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Library",
            "1",
            [new(ReusableLibraryResourceKinds.Dynamo, id)])));

        Assert.Contains("concrete project data", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (ReusableLibraryPackageService Service, byte[] Bytes) CreateTemplateLibrary()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var id = Guid.NewGuid();
        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(id, "template.pump", "Pump Template"));
        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Templates",
            "1",
            [new(ReusableLibraryResourceKinds.EquipmentTemplate, id)]));
        return (service, bytes);
    }

    private static byte[] RewriteManifest(
        byte[] packageBytes,
        Func<ReusableLibraryManifest, ReusableLibraryManifest> transform) =>
        RewriteArchive(packageBytes, (path, content) =>
        {
            if (path != ReusableLibraryPackageService.ManifestPath) return content;
            var manifest = JsonSerializer.Deserialize<ReusableLibraryManifest>(content, Json)!;
            return JsonSerializer.SerializeToUtf8Bytes(transform(manifest), Json);
        });

    private static byte[] RewriteArchive(
        byte[] packageBytes,
        Func<string, byte[], byte[]> transform,
        string? extraPath = null,
        byte[]? extraContent = null)
    {
        using var input = new MemoryStream(packageBytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var sourceStream = entry.Open();
                using var buffer = new MemoryStream();
                sourceStream.CopyTo(buffer);
                var content = transform(entry.FullName, buffer.ToArray());
                var targetEntry = target.CreateEntry(entry.FullName);
                using var targetStream = targetEntry.Open();
                targetStream.Write(content, 0, content.Length);
            }

            if (extraPath is not null)
            {
                var extra = target.CreateEntry(extraPath);
                using var stream = extra.Open();
                var content = extraContent ?? Array.Empty<byte>();
                stream.Write(content, 0, content.Length);
            }
        }

        return output.ToArray();
    }
}
