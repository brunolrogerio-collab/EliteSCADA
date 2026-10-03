using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.VisualAssets;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualDynamoSvgValidationTests
{
    [Fact]
    public void SvgSymbol_UnknownSemanticSlot_FailsClosedAgainstCanonicalAssetMetadata()
    {
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create(
            "image/svg+xml",
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path data-elitescada-slot=\"body\" d=\"M0 0h1v1z\"/></svg>"u8);
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        registry.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "asset.slot-authority",
            "Slot Authority",
            "slot-authority.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            Metadata: new Dictionary<string, string>
            {
                [StaticSvgInspector.MetadataSlotsKey] =
                    "[{\"name\":\"body\",\"fill\":true,\"stroke\":true,\"strokeWidth\":true}]"
            });
        registry.UpsertAsset(asset);

        var element = new VisualElementEngineeringDto(
            "pump-symbol",
            BuiltinVisualObjectSchemas.SvgSymbolType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.AssetRef] =
                    JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" }),
                [BuiltinVisualObjectSchemas.SvgPaintOverridesProperty] =
                    JsonSerializer.SerializeToElement(new
                    {
                        version = 1,
                        slots = new
                        {
                            body = new { fill = "#00AA00" },
                            invented = new { stroke = "#FF0000" }
                        }
                    })
            });

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.pump",
            Package(asset),
            registry);

        var issue = Assert.Single(issues, candidate => candidate.Code == "VISUAL_SVG_SLOT_UNKNOWN");
        Assert.True(issue.IsError);
        Assert.Contains("invented", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SvgSymbol_UnsupportedSemanticPaintProperty_FailsClosed()
    {
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        registry.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "asset.fill-only",
            "Fill Only",
            "fill-only.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            Metadata: new Dictionary<string, string>
            {
                [StaticSvgInspector.MetadataSlotsKey] =
                    "[{\"name\":\"body\",\"fill\":true,\"stroke\":false,\"strokeWidth\":false}]"
            });
        registry.UpsertAsset(asset);

        var element = new VisualElementEngineeringDto(
            "symbol",
            BuiltinVisualObjectSchemas.SvgSymbolType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.AssetRef] =
                    JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" }),
                [BuiltinVisualObjectSchemas.SvgPaintOverridesProperty] =
                    JsonSerializer.SerializeToElement(new
                    {
                        version = 1,
                        slots = new { body = new { stroke = "#FF0000" } }
                    })
            });

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.fill-only",
            Package(asset),
            registry);

        Assert.Contains(issues, candidate =>
            candidate.Code == "VISUAL_SVG_SLOT_PROPERTY_UNSUPPORTED" &&
            candidate.IsError);
    }

    [Fact]
    public void SvgSymbol_KnownSemanticSlotAndSupportedPaint_IsAccepted()
    {
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        registry.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "asset.body",
            "Body",
            "body.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            Metadata: new Dictionary<string, string>
            {
                [StaticSvgInspector.MetadataSlotsKey] =
                    "[{\"name\":\"body\",\"fill\":true,\"stroke\":true,\"strokeWidth\":true}]"
            });
        registry.UpsertAsset(asset);

        var element = new VisualElementEngineeringDto(
            "symbol",
            BuiltinVisualObjectSchemas.SvgSymbolType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.AssetRef] =
                    JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" }),
                [BuiltinVisualObjectSchemas.SvgPaintOverridesProperty] =
                    JsonSerializer.SerializeToElement(new
                    {
                        version = 1,
                        slots = new { body = new { fill = "#00AA00", stroke = "#111111", strokeWidth = 2.0 } }
                    })
            });

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.body",
            Package(asset),
            registry);

        Assert.Empty(issues);
    }

    [Fact]
    public void SvgSymbol_DynamicUnknownSemanticSlot_FailsClosed()
    {
        var (asset, registry) = AssetWithSlots(
            "[{\"name\":\"body\",\"fill\":true,\"stroke\":false,\"strokeWidth\":false}]");
        var element = DynamicBindingElement(asset.Id!.Value, "svg.slot.unknown.fill");

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.dynamic-slot",
            Package(asset),
            registry);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_SVG_SLOT_UNKNOWN" && issue.IsError);
    }

    [Fact]
    public void SvgSymbol_DynamicUnsupportedSemanticProperty_FailsClosed()
    {
        var (asset, registry) = AssetWithSlots(
            "[{\"name\":\"body\",\"fill\":true,\"stroke\":false,\"strokeWidth\":false}]");
        var element = DynamicBindingElement(asset.Id!.Value, "svg.slot.body.strokeWidth");

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.dynamic-slot",
            Package(asset),
            registry);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_SVG_SLOT_PROPERTY_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public void SvgSymbol_DynamicDeclaredSemanticProperty_IsAccepted()
    {
        var (asset, registry) = AssetWithSlots(
            "[{\"name\":\"body\",\"fill\":true,\"stroke\":false,\"strokeWidth\":false}]");
        var element = DynamicBindingElement(asset.Id!.Value, "svg.slot.body.fill");

        var issues = VisualAssetReferenceEngineeringValidation.Validate(
            element,
            ImportEntityKind.Dynamo,
            "dynamo.dynamic-slot",
            Package(asset),
            registry);

        Assert.Empty(issues);
    }

    private static VisualElementEngineeringDto DynamicBindingElement(Guid assetId, string destination) => new(
        "symbol",
        BuiltinVisualObjectSchemas.SvgSymbolType,
        Bindings:
        [
            new EngineeringBindingDto(
                destination,
                EngineeringBindingKind.Property,
                "{dynamoParameter:paint}")
        ],
        Properties: new Dictionary<string, JsonElement>
        {
            [VisualPropertyKeys.AssetRef] =
                JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
        });

    private static (VisualAssetEngineeringDto Asset, InMemoryVisualAssetEngineeringRegistry Registry) AssetWithSlots(string slots)
    {
        var assetId = Guid.NewGuid();
        var payload = VisualAssetPayload.Create("image/svg+xml", "<svg/>"u8);
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        registry.PutPayload(payload);
        var asset = new VisualAssetEngineeringDto(
            assetId,
            "asset.dynamic-slots",
            "Dynamic Slots",
            "dynamic-slots.svg",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            Metadata: new Dictionary<string, string>
            {
                [StaticSvgInspector.MetadataSlotsKey] = slots
            });
        registry.UpsertAsset(asset);
        return (asset, registry);
    }

    private static EngineeringPackage Package(VisualAssetEngineeringDto asset) => new(
        "scada.engineering",
        20,
        DateTimeOffset.UtcNow,
        Array.Empty<TagEngineeringDto>(),
        Array.Empty<AlarmEngineeringDto>(),
        VisualAssets: [asset]);
}
