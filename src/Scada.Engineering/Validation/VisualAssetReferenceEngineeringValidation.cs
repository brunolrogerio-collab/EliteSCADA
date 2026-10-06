using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.Validation;

/// <summary>
/// Canonical validation for Visual Asset references owned by visual objects.
/// SVG semantic paint overrides are checked against metadata derived from the
/// sanitized asset payload; arbitrary DOM/CSS selectors never enter this contract.
/// </summary>
internal static class VisualAssetReferenceEngineeringValidation
{
    public static IReadOnlyCollection<ImportIssue> Validate(
        VisualElementEngineeringDto element,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        IVisualAssetEngineeringRegistry visualAssets)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(visualAssets);

        var issues = new List<ImportIssue>();
        if (package.SchemaVersion < 13 ||
            (!element.Type.Equals(BuiltinVisualObjectSchemas.ImageType, StringComparison.Ordinal) &&
             !element.Type.Equals(BuiltinVisualObjectSchemas.Model3dType, StringComparison.Ordinal) &&
             !element.Type.Equals(BuiltinVisualObjectSchemas.SvgSymbolType, StringComparison.Ordinal) &&
             !element.Type.Equals(BuiltinVisualObjectSchemas.PdfViewerType, StringComparison.Ordinal) &&
             !element.Type.Equals(BuiltinVisualObjectSchemas.VideoPlayerType, StringComparison.Ordinal)) ||
            element.Properties is null ||
            !element.Properties.TryGetValue(VisualPropertyKeys.AssetRef, out var serialized) ||
            serialized.ValueKind == JsonValueKind.Null)
            return issues;

        if (!TryParseStableAssetId(serialized, out var assetId))
        {
            issues.Add(new(
                "VISUAL_ASSET_REFERENCE_ID_INVALID",
                $"Visual element '{element.Key}' assetRef must identify a stable Visual Asset GUID.",
                kind,
                entityKey,
                true));
            return issues;
        }

        var asset = visualAssets.FindAsset(assetId) ??
            (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                .FirstOrDefault(candidate => candidate is not null && candidate.Id == assetId);
        if (asset is null)
        {
            issues.Add(new(
                "VISUAL_ASSET_REFERENCE_NOT_FOUND",
                $"Visual element '{element.Key}' references Visual Asset 'asset:{assetId:D}', which does not exist in the prospective Engineering model.",
                kind,
                entityKey,
                true));
            return issues;
        }

        var mediaValid = element.Type switch
        {
            BuiltinVisualObjectSchemas.Model3dType => asset.MediaType is VisualAssetContentInspector.GlbMediaType or VisualAssetContentInspector.GltfMediaType,
            BuiltinVisualObjectSchemas.PdfViewerType => asset.MediaType.Equals(VisualAssetContentInspector.PdfMediaType, StringComparison.OrdinalIgnoreCase),
            BuiltinVisualObjectSchemas.VideoPlayerType => asset.MediaType is VisualAssetContentInspector.Mp4MediaType or VisualAssetContentInspector.WebmMediaType,
            _ => true
        };
        if (!mediaValid)
            issues.Add(new("VISUAL_MEDIA_ASSET_TYPE_INVALID", $"Visual element '{element.Key}' references an incompatible media asset.", kind, entityKey, true));

        if (!element.Type.Equals(BuiltinVisualObjectSchemas.SvgSymbolType, StringComparison.Ordinal))
            return issues;

        if (!asset.MediaType.Equals(VisualAssetContentInspector.SvgMediaType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(
                "VISUAL_SVG_ASSET_MEDIA_INVALID",
                $"SVG symbol '{element.Key}' must reference an image/svg+xml Visual Asset.",
                kind,
                entityKey,
                true));
            return issues;
        }

        ValidateSemanticPaintSlots(element, asset, kind, entityKey, issues);
        return issues;
    }

    private static bool TryParseStableAssetId(JsonElement serialized, out Guid assetId)
    {
        assetId = Guid.Empty;
        if (serialized.ValueKind != JsonValueKind.Object)
            return false;

        var fields = serialized.EnumerateObject().ToArray();
        if (fields.Length != 1 ||
            !fields[0].NameEquals("assetId") ||
            fields[0].Value.ValueKind != JsonValueKind.String)
            return false;

        var reference = fields[0].Value.GetString();
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var guidText = reference.StartsWith("asset:", StringComparison.Ordinal)
            ? reference["asset:".Length..]
            : reference;
        return Guid.TryParse(guidText, out assetId) && assetId != Guid.Empty;
    }

    private static void ValidateSemanticPaintSlots(
        VisualElementEngineeringDto element,
        VisualAssetEngineeringDto asset,
        ImportEntityKind kind,
        string entityKey,
        ICollection<ImportIssue> issues)
    {
        var declared = ReadDeclaredSlots(asset);

        if (element.Properties is not null &&
            element.Properties.TryGetValue(BuiltinVisualObjectSchemas.SvgPaintOverridesProperty, out var overrides) &&
            overrides.ValueKind == JsonValueKind.Object &&
            overrides.TryGetProperty("slots", out var slots) &&
            slots.ValueKind == JsonValueKind.Object)
        {
            foreach (var slot in slots.EnumerateObject())
            {
                if (!declared.TryGetValue(slot.Name, out var capabilities))
                {
                    issues.Add(new(
                        "VISUAL_SVG_SLOT_UNKNOWN",
                        $"SVG symbol '{element.Key}' paint override references semantic slot '{slot.Name}', which is not declared by Visual Asset '{asset.Key}'.",
                        kind,
                        entityKey,
                        true));
                    continue;
                }

                if (slot.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var property in slot.Value.EnumerateObject())
                {
                    if (Supports(capabilities, property.Name)) continue;
                    issues.Add(new(
                        "VISUAL_SVG_SLOT_PROPERTY_UNSUPPORTED",
                        $"SVG symbol '{element.Key}' semantic slot '{slot.Name}' does not expose '{property.Name}' in canonical Visual Asset metadata.",
                        kind,
                        entityKey,
                        true));
                }
            }
        }

        foreach (var destinationKey in DynamicSemanticDestinations(element))
        {
            if (!SvgSemanticDynamicDestinations.TryParse(destinationKey, out var destination))
                continue;

            if (!declared.TryGetValue(destination.Slot, out var capabilities))
            {
                issues.Add(new(
                    "VISUAL_SVG_SLOT_UNKNOWN",
                    $"SVG symbol '{element.Key}' dynamic destination '{destinationKey}' references semantic slot '{destination.Slot}', which is not declared by Visual Asset '{asset.Key}'.",
                    kind,
                    entityKey,
                    true));
                continue;
            }

            var propertyName = destination.Property switch
            {
                SvgSemanticDynamicPaintProperty.Fill => "fill",
                SvgSemanticDynamicPaintProperty.Stroke => "stroke",
                SvgSemanticDynamicPaintProperty.StrokeWidth => "strokeWidth",
                _ => string.Empty
            };
            if (Supports(capabilities, propertyName))
                continue;

            issues.Add(new(
                "VISUAL_SVG_SLOT_PROPERTY_UNSUPPORTED",
                $"SVG symbol '{element.Key}' dynamic destination '{destinationKey}' is not supported by semantic slot '{destination.Slot}' in canonical Visual Asset metadata.",
                kind,
                entityKey,
                true));
        }
    }

    private static IEnumerable<string> DynamicSemanticDestinations(VisualElementEngineeringDto element)
    {
        foreach (var binding in element.Bindings ?? Array.Empty<EngineeringBindingDto>())
            if (binding is not null && !string.IsNullOrWhiteSpace(binding.Key))
                yield return binding.Key;
        foreach (var map in element.PropertyMaps ?? Array.Empty<VisualPropertyMapEngineeringDto>())
            if (map is not null && !string.IsNullOrWhiteSpace(map.PropertyKey))
                yield return map.PropertyKey;
        foreach (var expression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
            if (expression is not null && !string.IsNullOrWhiteSpace(expression.PropertyKey))
                yield return expression.PropertyKey;
        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
            if (condition is not null && !string.IsNullOrWhiteSpace(condition.PropertyKey))
                yield return condition.PropertyKey;
    }

    private static bool Supports(SvgSlotCapabilities capabilities, string property) => property switch
    {
        "fill" => capabilities.Fill,
        "stroke" => capabilities.Stroke,
        "strokeWidth" => capabilities.StrokeWidth,
        _ => false
    };

    private static IReadOnlyDictionary<string, SvgSlotCapabilities> ReadDeclaredSlots(VisualAssetEngineeringDto asset)
    {
        if (asset.Metadata is null ||
            !asset.Metadata.TryGetValue(StaticSvgInspector.MetadataSlotsKey, out var serialized) ||
            string.IsNullOrWhiteSpace(serialized))
            return new Dictionary<string, SvgSlotCapabilities>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(serialized);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, SvgSlotCapabilities>(StringComparer.Ordinal);

            var result = new Dictionary<string, SvgSlotCapabilities>(StringComparer.Ordinal);
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String)
                    continue;

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result[name] = new SvgSlotCapabilities(
                    ReadBoolean(entry, "fill"),
                    ReadBoolean(entry, "stroke"),
                    ReadBoolean(entry, "strokeWidth"));
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, SvgSlotCapabilities>(StringComparer.Ordinal);
        }
    }

    private static bool ReadBoolean(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private sealed record SvgSlotCapabilities(bool Fill, bool Stroke, bool StrokeWidth);
}
