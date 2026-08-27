using Scada.Core.Tags;

namespace Scada.Historian.Policies;

/// <summary>
/// Runtime capture gate for TAG events. Existing legacy TAGs that do not carry
/// an explicit historian.enabled metadata key retain the historical capture-all
/// behavior. Engineering TAGs that declare the key are captured only when it is
/// explicitly true.
/// </summary>
public static class HistorianCapturePolicy
{
    public const string EnabledMetadataKey = "historian.enabled";

    public static bool ShouldCapture(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Metadata is null || !tag.Metadata.TryGetValue(EnabledMetadataKey, out var configured))
            return true;

        return bool.TryParse(configured, out var enabled) && enabled;
    }
}
