using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualAssets;

public static partial class VisualAssetEngineeringValidator
{
    public const long MaximumPayloadBytes = 16L * 1024L * 1024L;
    public const long MaximumVideoPayloadBytes = 64L * 1024L * 1024L;
    public const long MaximumPdfPayloadBytes = 32L * 1024L * 1024L;
    public const int MaximumPixelDimension = 16_384;

    private static readonly HashSet<string> SupportedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/bmp",
        VisualAssetContentInspector.SvgMediaType,
        VisualAssetContentInspector.PdfMediaType,
        VisualAssetContentInspector.Mp4MediaType,
        VisualAssetContentInspector.WebmMediaType,
        VisualAssetContentInspector.GlbMediaType,
        VisualAssetContentInspector.GltfMediaType
    };

    public static IReadOnlyCollection<ImportIssue> Validate(
        VisualAssetEngineeringDto? asset,
        IVisualAssetEngineeringRegistry registry,
        EngineeringImportContext? context = null)
    {
        var key = asset?.Key ?? "<null>";
        var issues = new List<ImportIssue>();

        if (asset is null)
        {
            issues.Add(Error("VISUAL_ASSET_NULL", "Visual asset entry cannot be null.", key));
            return issues;
        }

        if (asset.Id == Guid.Empty)
            issues.Add(Error("VISUAL_ASSET_ID_EMPTY", "Visual asset ID cannot be an empty GUID.", key));
        if (string.IsNullOrWhiteSpace(asset.Key) || asset.Key.Length > 128)
            issues.Add(Error("VISUAL_ASSET_KEY_INVALID", "Visual asset key is required and must be at most 128 characters.", key));
        if (string.IsNullOrWhiteSpace(asset.Name) || asset.Name.Length > 256)
            issues.Add(Error("VISUAL_ASSET_NAME_INVALID", "Visual asset name is required and must be at most 256 characters.", key));
        if (string.IsNullOrWhiteSpace(asset.OriginalFileName) || asset.OriginalFileName.Length > 512)
            issues.Add(Error("VISUAL_ASSET_FILENAME_INVALID", "Visual asset original filename is required and must be at most 512 characters.", key));
        if (asset.OriginalFileName.IndexOfAny(['\r', '\n', '\0']) >= 0)
            issues.Add(Error("VISUAL_ASSET_FILENAME_CONTROL", "Visual asset original filename contains unsupported control characters.", key));
        if (!SupportedMediaTypes.Contains(asset.MediaType))
            issues.Add(Error("VISUAL_ASSET_MEDIA_TYPE_UNSUPPORTED", $"Visual asset media type '{asset.MediaType}' is not supported.", key));
        var maximumBytes = MaximumBytesFor(asset.MediaType);
        if (asset.ByteLength <= 0 || asset.ByteLength > maximumBytes)
            issues.Add(Error("VISUAL_ASSET_SIZE_INVALID", $"Visual asset payload size must be between 1 and {maximumBytes} bytes for this media type.", key));
        if (string.IsNullOrWhiteSpace(asset.Sha256) || !Sha256Regex().IsMatch(asset.Sha256))
            issues.Add(Error("VISUAL_ASSET_HASH_INVALID", "Visual asset SHA-256 must be exactly 64 hexadecimal characters.", key));

        if (asset.PixelWidth.HasValue != asset.PixelHeight.HasValue)
            issues.Add(Error("VISUAL_ASSET_DIMENSIONS_PARTIAL", "Visual asset pixel width and height must either both be present or both be absent.", key));
        if (asset.PixelWidth is <= 0 or > MaximumPixelDimension ||
            asset.PixelHeight is <= 0 or > MaximumPixelDimension)
            issues.Add(Error("VISUAL_ASSET_DIMENSIONS_INVALID", $"Visual asset dimensions must be between 1 and {MaximumPixelDimension} pixels.", key));

        if (issues.Any(x => x.IsError))
            return issues;

        var normalizedHash = asset.Sha256.ToLowerInvariant();
        var payload = context is not null && context.TryGetVisualAssetPayload(normalizedHash, out var supplied)
            ? supplied
            : registry.FindPayload(normalizedHash);

        if (payload is null)
        {
            issues.Add(Error(
                "VISUAL_ASSET_PAYLOAD_MISSING",
                $"Visual asset payload '{normalizedHash}' is not available in the project or import package.",
                key));
            return issues;
        }

        if (!payload.MediaType.Equals(asset.MediaType, StringComparison.OrdinalIgnoreCase))
            issues.Add(Error("VISUAL_ASSET_PAYLOAD_MEDIA_MISMATCH", "Visual asset payload media type does not match canonical metadata.", key));
        if (payload.ByteLength != asset.ByteLength)
            issues.Add(Error("VISUAL_ASSET_PAYLOAD_LENGTH_MISMATCH", "Visual asset payload length does not match canonical metadata.", key));

        var actualHash = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
        if (!actualHash.Equals(normalizedHash, StringComparison.Ordinal))
            issues.Add(Error("VISUAL_ASSET_PAYLOAD_HASH_MISMATCH", "Visual asset payload SHA-256 does not match canonical metadata.", key));

        try
        {
            var inspection = VisualAssetContentInspector.InspectAndCanonicalize(payload.Content);
            if (!inspection.MediaType.Equals(asset.MediaType, StringComparison.OrdinalIgnoreCase) ||
                !inspection.MediaType.Equals(payload.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "VISUAL_ASSET_PAYLOAD_SIGNATURE_MISMATCH",
                    "Visual asset detected format does not match canonical media metadata.",
                    key));
            }

            if (inspection.MediaType.Equals(VisualAssetContentInspector.SvgMediaType, StringComparison.OrdinalIgnoreCase) &&
                !inspection.CanonicalContent.AsSpan().SequenceEqual(payload.Content))
            {
                issues.Add(Error(
                    "VISUAL_ASSET_SVG_NOT_CANONICAL",
                    "SVG payload is safe but is not stored in the canonical sanitized representation.",
                    key));
            }

            ValidateSvgMetadata(asset, inspection, issues, key);

            if ((inspection.PixelWidth.HasValue && asset.PixelWidth.HasValue && asset.PixelWidth.Value != inspection.PixelWidth.Value) ||
                (inspection.PixelHeight.HasValue && asset.PixelHeight.HasValue && asset.PixelHeight.Value != inspection.PixelHeight.Value) ||
                (!inspection.PixelWidth.HasValue && asset.PixelWidth.HasValue) ||
                (!inspection.PixelHeight.HasValue && asset.PixelHeight.HasValue))
            {
                issues.Add(Error(
                    "VISUAL_ASSET_PAYLOAD_DIMENSIONS_MISMATCH",
                    "Visual asset detected dimensions do not match canonical metadata.",
                    key));
            }
        }
        catch (InvalidDataException ex)
        {
            issues.Add(Error(
                "VISUAL_ASSET_PAYLOAD_INVALID",
                $"Visual asset payload is not a structurally valid supported image or media file: {ex.Message}",
                key));
        }

        return issues;
    }

    private static void ValidateSvgMetadata(
        VisualAssetEngineeringDto asset,
        VisualAssetContentInspection inspection,
        ICollection<ImportIssue> issues,
        string key)
    {
        var reserved = (asset.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Where(pair => pair.Key.StartsWith(StaticSvgInspector.MetadataPrefix, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        if (!inspection.MediaType.Equals(VisualAssetContentInspector.SvgMediaType, StringComparison.OrdinalIgnoreCase))
        {
            if (reserved.Count > 0)
                issues.Add(Error("VISUAL_ASSET_SVG_METADATA_ON_RASTER", "Raster assets cannot carry reserved SVG metadata.", key));
            return;
        }

        if (reserved.Count == 0)
            return; // Legacy SVG imported before V2 remains valid.

        var expected = inspection.CanonicalMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        if (reserved.Count != expected.Count ||
            expected.Any(pair => !reserved.TryGetValue(pair.Key, out var value) || !value.Equals(pair.Value, StringComparison.Ordinal)))
            issues.Add(Error("VISUAL_ASSET_SVG_METADATA_MISMATCH", "SVG paint metadata does not match the canonical sanitized payload.", key));
    }

    private static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.VisualAsset, key, true);

    public static long MaximumBytesFor(string? mediaType) => mediaType?.ToLowerInvariant() switch
    {
        VisualAssetContentInspector.PdfMediaType => MaximumPdfPayloadBytes,
        VisualAssetContentInspector.Mp4MediaType or VisualAssetContentInspector.WebmMediaType => MaximumVideoPayloadBytes,
        VisualAssetContentInspector.GlbMediaType or VisualAssetContentInspector.GltfMediaType => MaximumModelPayloadBytes,
        _ => MaximumPayloadBytes
    };

    public const long MaximumModelPayloadBytes = 32L * 1024L * 1024L;

    [GeneratedRegex("^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}
