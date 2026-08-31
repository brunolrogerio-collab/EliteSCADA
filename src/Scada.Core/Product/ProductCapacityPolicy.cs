namespace Scada.Core.Product;

/// <summary>
/// Central product-capacity policy for the externally distributed Preview edition.
/// Keep edition limits here so later product tiers can change capacity without
/// scattering magic numbers through Engineering, Drivers or UI code.
/// </summary>
public static class ProductCapacityPolicy
{
    public const string EditionName = "Preview";
    public const int MaxTagsPerProject = 200;
    public const string TagLimitIssueCode = "PRODUCT_TAG_LIMIT_EXCEEDED";

    public static bool AllowsTagCount(int count) =>
        count >= 0 && count <= MaxTagsPerProject;

    public static string TagLimitMessage(int requestedCount) =>
        $"{EditionName} edition supports up to {MaxTagsPerProject} TAGs per project. " +
        $"The operation would result in {requestedCount} TAGs.";

    public static void EnsureTagCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (AllowsTagCount(count))
            return;

        throw new ProductCapacityExceededException(count, MaxTagsPerProject, TagLimitMessage(count));
    }
}

public sealed class ProductCapacityExceededException : InvalidOperationException
{
    public ProductCapacityExceededException(int requestedCount, int maximumCount, string message)
        : base(message)
    {
        RequestedCount = requestedCount;
        MaximumCount = maximumCount;
    }

    public int RequestedCount { get; }
    public int MaximumCount { get; }
}
