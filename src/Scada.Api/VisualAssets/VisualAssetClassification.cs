using Scada.Engineering.Contracts;

namespace Scada.Api.VisualAssets;

public static class VisualAssetClassification
{
    public const string DynamoArtworkRole = "dynamoArtwork";
    private const string DynamoArtworkOrigin = "original-elitescada-vector-factory";

    public static bool IsDynamoArtwork(VisualAssetEngineeringDto? asset)
    {
        if (asset?.Metadata is not { } metadata) return false;
        return metadata.TryGetValue("assetRole", out var role) &&
                   role.Equals(DynamoArtworkRole, StringComparison.OrdinalIgnoreCase) ||
               metadata.TryGetValue("assetOrigin", out var origin) &&
                   origin.Equals(DynamoArtworkOrigin, StringComparison.OrdinalIgnoreCase);
    }

    public static int CountUserAssets(IEnumerable<VisualAssetEngineeringDto>? assets) =>
        assets?.Count(asset => !IsDynamoArtwork(asset)) ?? 0;
}
