using System.Buffers.Binary;
using System.IO.Compression;
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
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class VisualAssetWave08Tests
{
    [Fact]
    public void RasterInspector_AcceptsSupportedCompleteRasterFamilies()
    {
        var png = RasterImageInspector.Inspect(CreatePng(2, 3));
        var jpeg = RasterImageInspector.Inspect(CreateJpeg(3, 2));
        var bmp = RasterImageInspector.Inspect(CreateBmp(1, 1));

        Assert.Equal(new RasterImageInspection("image/png", 2, 3), png);
        Assert.Equal(new RasterImageInspection("image/jpeg", 3, 2), jpeg);
        Assert.Equal(new RasterImageInspection("image/bmp", 1, 1), bmp);
    }

    [Fact]
    public void RasterInspector_RejectsStructurallyTruncatedPayloads()
    {
        var png = CreatePng(2, 3)[..^12];
        var jpeg = CreateJpeg(3, 2)[..^2];
        var bmp = CreateBmp(1, 1);
        bmp[2]++;

        Assert.Throws<InvalidDataException>(() => RasterImageInspector.Inspect(png));
        Assert.Throws<InvalidDataException>(() => RasterImageInspector.Inspect(jpeg));
        Assert.Throws<InvalidDataException>(() => RasterImageInspector.Inspect(bmp));
    }

    [Fact]
    public void AssetValidator_RejectsMatchingHashWhenRasterStructureIsInvalid()
    {
        var invalidPng = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0 };
        var payload = VisualAssetPayload.Create("image/png", invalidPng);
        var asset = new VisualAssetEngineeringDto(
            Guid.NewGuid(),
            "logo",
            "Logo",
            "logo.png",
            "image/png",
            payload.ByteLength,
            payload.Sha256,
            1,
            1);
        var context = new EngineeringImportContext(
            new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase)
            {
                [payload.Sha256] = payload
            });

        var issues = VisualAssetEngineeringValidator.Validate(
            asset,
            new InMemoryVisualAssetEngineeringRegistry(),
            context);

        Assert.Contains(issues, x => x.Code == "VISUAL_ASSET_PAYLOAD_INVALID" && x.IsError);
    }

    [Fact]
    public void CurrentSchema_ExportsAndParsesFirstClassVisualAssetMetadata()
    {
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/png", CreatePng(2, 3));
        var asset = CreateAsset(Guid.NewGuid(), "logo", "Logo", "logo.png", payload);
        registry.PutPayload(payload);
        registry.UpsertAsset(asset);

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(alarms, registry);

        var parsed = exchange.ParseJson(exchange.ExportJson(indented: false));

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, parsed.SchemaVersion);
        var roundTripped = Assert.Single(parsed.VisualAssets!);
        Assert.Equal(asset.Id, roundTripped.Id);
        Assert.Equal(asset.Sha256, roundTripped.Sha256);
        Assert.Equal(2, roundTripped.PixelWidth);
        Assert.Equal(3, roundTripped.PixelHeight);
    }

    [Fact]
    public void SchemaV13_CoreImageAssetRef_ResolvesProspectiveStableAssetIdAndRejectsUnknownId()
    {
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(alarms, registry);
        var payload = VisualAssetPayload.Create("image/png", CreatePng(2, 3));
        var assetId = Guid.NewGuid();
        var asset = CreateAsset(assetId, "logo", "Logo", "logo.png", payload);
        var screen = CreateImageScreen(assetId);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: new[] { screen },
            VisualAssets: new[] { asset });
        var context = new EngineeringImportContext(
            new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase)
            {
                [payload.Sha256] = payload
            });

        var valid = exchange.Preview(package, ImportMode.CreateAndUpdate, context);
        var missing = exchange.Preview(
            package with
            {
                Screens = new[] { CreateImageScreen(Guid.NewGuid()) },
                VisualAssets = Array.Empty<VisualAssetEngineeringDto>()
            },
            ImportMode.CreateAndUpdate,
            EngineeringImportContext.Empty);

        Assert.True(valid.CanApply);
        Assert.DoesNotContain(valid.Items.SelectMany(x => x.Issues), x => x.Code.StartsWith("VISUAL_ASSET_REFERENCE_", StringComparison.Ordinal));
        Assert.Contains(missing.Items.SelectMany(x => x.Issues), x => x.Code == "VISUAL_ASSET_REFERENCE_NOT_FOUND");
    }

    [Fact]
    public void ProjectPackageV2_RoundTripsAssetsWithoutInspectMutatingTargetWorking()
    {
        var sourceAssets = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/png", CreatePng(2, 3));
        var asset = CreateAsset(Guid.NewGuid(), "logo", "Logo", "logo.png", payload);
        sourceAssets.PutPayload(payload);
        sourceAssets.UpsertAsset(asset);

        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceExchange = CreateExchange(sourceAlarms, sourceAssets);
        var sourcePackages = new ProjectPackageService(sourceExchange, sourceAssets);
        var bytes = sourcePackages.Export("plant-a", "Plant A");

        var targetAssets = new InMemoryVisualAssetEngineeringRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetExchange = CreateExchange(targetAlarms, targetAssets);
        var targetPackages = new ProjectPackageService(targetExchange, targetAssets);

        var inspection = targetPackages.Inspect(bytes);
        Assert.Equal(2, inspection.Manifest.FormatVersion);
        Assert.Equal(2, inspection.Manifest.Files.Count);
        Assert.Contains(inspection.Manifest.Files, x => x.Path == $"assets/{payload.Sha256}");
        Assert.Empty(targetAssets.SnapshotAssets());
        Assert.False(targetAssets.HasPayload(payload.Sha256));

        var preview = targetPackages.Preview(bytes, ImportMode.CreateAndUpdate);
        var applied = targetPackages.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.DoesNotContain(applied.Issues, x => x.IsError);
        var restored = Assert.Single(targetAssets.SnapshotAssets());
        Assert.Equal(asset.Id, restored.Id);
        Assert.Equal(payload.Content, targetAssets.FindPayload(payload.Sha256)!.Content);
    }

    [Fact]
    public void ProjectPackageV2_RejectsMissingAssetSidecar()
    {
        var assets = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/png", CreatePng(2, 3));
        var asset = CreateAsset(Guid.NewGuid(), "logo", "Logo", "logo.png", payload);
        assets.PutPayload(payload);
        assets.UpsertAsset(asset);

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(alarms, assets);
        var service = new ProjectPackageService(exchange, assets);
        var withoutSidecar = RemoveAssetSidecars(service.Export("plant-a", "Plant A"));

        Assert.Throws<InvalidDataException>(() => service.Inspect(withoutSidecar));
    }

    [Fact]
    public void ProjectPackageV1_RemainsReadableForAssetFreeProjects()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new ProjectPackageService(new EngineeringExchangeService(new InMemoryTagRegistry(), alarms));
        var v1 = RewriteAsV1(service.Export("plant-a", "Plant A"));

        var inspection = service.Inspect(v1);

        Assert.Equal(1, inspection.Manifest.FormatVersion);
        Assert.Empty(inspection.Engineering.VisualAssets!);
    }

    private static EngineeringExchangeService CreateExchange(
        InMemoryAlarmEngine alarms,
        IVisualAssetEngineeringRegistry visualAssets) =>
        new(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            new InMemoryScriptEngineeringRegistry(),
            visualAssets);

    private static VisualAssetEngineeringDto CreateAsset(
        Guid id,
        string key,
        string name,
        string originalFileName,
        VisualAssetPayload payload)
    {
        var inspection = RasterImageInspector.Inspect(payload.Content);
        return new VisualAssetEngineeringDto(
            id,
            key,
            name,
            originalFileName,
            inspection.MediaType,
            payload.ByteLength,
            payload.Sha256,
            inspection.PixelWidth,
            inspection.PixelHeight);
    }

    private static ScreenEngineeringDto CreateImageScreen(Guid assetId) =>
        new(
            Guid.NewGuid(),
            "overview",
            "Overview",
            Elements: new[]
            {
                new VisualElementEngineeringDto(
                    "logo",
                    "core.image",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    },
                    Id: Guid.NewGuid())
            });

    private static byte[] CreatePng(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), checked((uint)height));
        ihdr[8] = 8;
        ihdr[9] = 6;
        WritePngChunk(output, "IHDR"u8, ihdr);
        WritePngChunk(output, "IDAT"u8, [0]);
        WritePngChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        output.Write(length);
        output.Write(type);
        output.Write(data);
        output.Write([0, 0, 0, 0]); // CRC is not decoded by the bounded structural inspector.
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([0xFF, 0xD8]);
        output.Write([0xFF, 0xC0, 0x00, 0x0B, 0x08]);
        Span<byte> dimensions = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(dimensions.Slice(0, 2), checked((ushort)height));
        BinaryPrimitives.WriteUInt16BigEndian(dimensions.Slice(2, 2), checked((ushort)width));
        output.Write(dimensions);
        output.Write([0x01, 0x01, 0x11, 0x00]);
        output.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        output.WriteByte(0x00);
        output.Write([0xFF, 0xD9]);
        return output.ToArray();
    }

    private static byte[] CreateBmp(int width, int height)
    {
        const int pixelOffset = 54;
        var rowBytes = checked(((width * 3 + 3) / 4) * 4);
        var pixelBytes = checked(rowBytes * height);
        var fileSize = checked(pixelOffset + pixelBytes);
        var bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), checked((uint)fileSize));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10, 4), pixelOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), height);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34, 4), checked((uint)pixelBytes));
        return bytes;
    }

    private static byte[] RemoveAssetSidecars(byte[] packageBytes)
    {
        using var input = new MemoryStream(packageBytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries.Where(x => !x.FullName.StartsWith("assets/", StringComparison.Ordinal)))
            {
                var copied = target.CreateEntry(entry.FullName);
                using var sourceStream = entry.Open();
                using var targetStream = copied.Open();
                sourceStream.CopyTo(targetStream);
            }
        }
        return output.ToArray();
    }

    private static byte[] RewriteAsV1(byte[] packageBytes)
    {
        var json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        using var input = new MemoryStream(packageBytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        byte[] engineering;
        using (var stream = source.GetEntry(ProjectPackageService.EngineeringPath)!.Open())
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            engineering = buffer.ToArray();
        }

        ProjectPackageManifest manifest;
        using (var stream = source.GetEntry(ProjectPackageService.ManifestPath)!.Open())
            manifest = JsonSerializer.Deserialize<ProjectPackageManifest>(stream, json)!;
        manifest = manifest with { FormatVersion = 1 };

        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = target.CreateEntry(ProjectPackageService.ManifestPath);
            using (var stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, manifest, json);

            var engineeringEntry = target.CreateEntry(ProjectPackageService.EngineeringPath);
            using var engineeringStream = engineeringEntry.Open();
            engineeringStream.Write(engineering, 0, engineering.Length);
        }
        return output.ToArray();
    }
}
