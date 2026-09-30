using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Branding;

public interface IApplicationBrandingEngineeringRegistry
{
    ApplicationBrandingEngineeringDto? Snapshot();
    void Replace(ApplicationBrandingEngineeringDto? branding);
    void Clear();
}

public sealed class InMemoryApplicationBrandingEngineeringRegistry : IApplicationBrandingEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Action? _changed;
    private ApplicationBrandingEngineeringDto? _branding;

    public InMemoryApplicationBrandingEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public ApplicationBrandingEngineeringDto? Snapshot()
    {
        lock (_sync)
            return _branding;
    }

    public void Replace(ApplicationBrandingEngineeringDto? branding)
    {
        bool changed;
        lock (_sync)
        {
            changed = !Equals(_branding, branding);
            _branding = branding;
        }

        if (changed) _changed?.Invoke();
    }

    public void Clear() => Replace(null);
}

public static class ApplicationBrandingEngineeringValidator
{
    public const int MaximumTextLength = 128;
    public const int MaximumSubtitleLength = 256;

    public static IReadOnlyCollection<ImportIssue> Validate(
        ApplicationBrandingEngineeringDto? branding,
        IVisualAssetEngineeringRegistry visualAssets,
        IReadOnlyCollection<VisualAssetEngineeringDto>? prospectiveAssets = null)
    {
        if (branding is null)
            return Array.Empty<ImportIssue>();

        var issues = new List<ImportIssue>();
        if (branding.Text is { Length: > MaximumTextLength } ||
            branding.Text?.Any(char.IsControl) == true)
        {
            issues.Add(Error(
                "BRANDING_TEXT_INVALID",
                $"Branding text must be a single plain-text value of at most {MaximumTextLength} characters."));
        }

        if (branding.Subtitle is { Length: > MaximumSubtitleLength } ||
            branding.Subtitle?.Any(char.IsControl) == true)
        {
            issues.Add(Error(
                "BRANDING_SUBTITLE_INVALID",
                $"Branding subtitle must be a single plain-text value of at most {MaximumSubtitleLength} characters."));
        }

        if (branding.Mode == ApplicationBrandingMode.Text &&
            string.IsNullOrWhiteSpace(branding.Text))
        {
            issues.Add(Error(
                "BRANDING_TEXT_REQUIRED",
                "TEXT branding requires a non-empty plain-text application name."));
        }

        if (branding.Mode == ApplicationBrandingMode.Image)
        {
            if (!branding.VisualAssetId.HasValue || branding.VisualAssetId == Guid.Empty)
            {
                issues.Add(Error(
                    "BRANDING_IMAGE_ASSET_REQUIRED",
                    "IMAGE branding requires a stable canonical VisualAsset identity."));
            }
            else
            {
                var assetId = branding.VisualAssetId.Value;
                var exists = visualAssets.FindAsset(assetId) is not null ||
                    (prospectiveAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                        .Any(asset => asset is not null && asset.Id == assetId);
                if (!exists)
                {
                    issues.Add(Error(
                        "BRANDING_IMAGE_ASSET_NOT_FOUND",
                        $"IMAGE branding references VisualAsset '{assetId:D}', which does not exist in the prospective Engineering model."));
                }
            }
        }

        return issues;
    }

    private static ImportIssue Error(string code, string message) =>
        new(code, message, ImportEntityKind.Branding, "application-branding", true);
}
