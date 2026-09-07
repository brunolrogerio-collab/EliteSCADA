using System.IO.Compression;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryProvenanceExportTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Export_StripsPreviousOriginFromCanonicalResourcePayloads()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "template.reexport",
            "Template",
            Metadata: OriginMetadata("template")));
        assets.UpsertDynamo(new DynamoEngineeringDto(
            dynamoId,
            "dynamo.reexport",
            "Dynamo",
            Metadata: OriginMetadata("dynamo")));

        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        visualAssets.PutPayload(payload);
        visualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.reexport",
            "Asset",
            "asset.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            Metadata: OriginMetadata("asset")));

        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "New Library",
            "1.0.0",
            [
                new(ReusableLibraryResourceKinds.EquipmentTemplate, templateId),
                new(ReusableLibraryResourceKinds.Dynamo, dynamoId),
                new(ReusableLibraryResourceKinds.VisualAsset, assetId)
            ]));
        var inspection = service.Inspect(bytes);

        using var input = new MemoryStream(bytes);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        foreach (var resource in inspection.Manifest.Resources)
        {
            var entry = archive.GetEntry(resource.PayloadPath);
            Assert.NotNull(entry);
            using var stream = entry!.Open();
            IReadOnlyDictionary<string, string>? metadata = resource.Kind switch
            {
                ReusableLibraryResourceKinds.EquipmentTemplate =>
                    JsonSerializer.Deserialize<EquipmentTemplateEngineeringDto>(stream, Json)!.Metadata,
                ReusableLibraryResourceKinds.Dynamo =>
                    JsonSerializer.Deserialize<DynamoEngineeringDto>(stream, Json)!.Metadata,
                ReusableLibraryResourceKinds.VisualAsset =>
                    JsonSerializer.Deserialize<VisualAssetEngineeringDto>(stream, Json)!.Metadata,
                _ => throw new InvalidOperationException(resource.Kind)
            };

            Assert.NotNull(metadata);
            Assert.Equal(resource.Kind switch
            {
                ReusableLibraryResourceKinds.EquipmentTemplate => "template",
                ReusableLibraryResourceKinds.Dynamo => "dynamo",
                ReusableLibraryResourceKinds.VisualAsset => "asset",
                _ => throw new InvalidOperationException(resource.Kind)
            }, metadata!["owner"]);
            Assert.DoesNotContain(metadata.Keys, ReusableLibraryProvenance.IsOriginKey);
        }
    }

    private static Dictionary<string, string> OriginMetadata(string owner) => new()
    {
        ["owner"] = owner,
        [ReusableLibraryProvenance.LibraryIdKey] = Guid.NewGuid().ToString("D"),
        [ReusableLibraryProvenance.LibraryVersionKey] = "old",
        [ReusableLibraryProvenance.ResourceIdKey] = Guid.NewGuid().ToString("D"),
        [ReusableLibraryProvenance.ResourceKindKey] = "old-kind",
        [ReusableLibraryProvenance.PayloadSha256Key] = new string('b', 64)
    };
}
