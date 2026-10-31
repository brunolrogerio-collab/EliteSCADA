namespace Scada.Security.Authorization;

/// <summary>
/// Explicit read-only resources an unauthenticated Runtime visitor may see.
/// The policy belongs to Security Authority and never travels in an Engineering package.
/// </summary>
public sealed record RuntimePublicAccessPolicy(
    bool Enabled,
    Guid? StartScreenId,
    IReadOnlyCollection<Guid> ScreenIds,
    IReadOnlyCollection<Guid> PopupIds,
    IReadOnlyCollection<Guid> TagIds,
    IReadOnlyCollection<Guid> VisualAssetIds,
    string? ProjectKey = null,
    long? Revision = null)
{
    public static RuntimePublicAccessPolicy Disabled { get; } = new(
        false, null, Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

    public static RuntimePublicAccessPolicy Normalize(RuntimePublicAccessPolicy? policy)
    {
        if (policy is null) return Disabled;
        return policy with
        {
            ScreenIds = NormalizeIds(policy.ScreenIds),
            PopupIds = NormalizeIds(policy.PopupIds),
            TagIds = NormalizeIds(policy.TagIds),
            VisualAssetIds = NormalizeIds(policy.VisualAssetIds),
            ProjectKey = string.IsNullOrWhiteSpace(policy.ProjectKey) ? null : policy.ProjectKey.Trim()
        };
    }

    public static void Validate(RuntimePublicAccessPolicy? policy)
    {
        if (policy is null) return;
        ValidateIds(policy.ScreenIds, "screenIds");
        ValidateIds(policy.PopupIds, "popupIds");
        ValidateIds(policy.TagIds, "tagIds");
        ValidateIds(policy.VisualAssetIds, "visualAssetIds");

        var screens = policy.ScreenIds.ToHashSet();
        if (policy.StartScreenId is { } start && (start == Guid.Empty || !screens.Contains(start)))
            throw new InvalidDataException("Public Runtime startScreenId must identify one of the explicitly exposed screens.");
        if (policy.Enabled && (!policy.StartScreenId.HasValue || screens.Count == 0 ||
            string.IsNullOrWhiteSpace(policy.ProjectKey) || policy.Revision is null or <= 0))
            throw new InvalidDataException("Public Runtime requires an exposed screen, a start screen, and its exact Active project revision before it can be enabled.");
    }

    private static Guid[] NormalizeIds(IEnumerable<Guid>? ids) =>
        (ids ?? Array.Empty<Guid>()).Distinct().Order().ToArray();

    private static void ValidateIds(IEnumerable<Guid>? ids, string field)
    {
        if (ids is null) throw new InvalidDataException($"Public Runtime {field} is required.");
        var values = ids.ToArray();
        if (values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length)
            throw new InvalidDataException($"Public Runtime {field} must contain unique, non-empty stable IDs.");
    }
}
