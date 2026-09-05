using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Host-owned reconciliation between stable TAG DataSourceId identity and the
/// legacy Source key still consumed by runtime planners. A stable ID is
/// authoritative when present. Legacy key matching is used only when no stable
/// identity exists, and an orphaned/mismatched ID can never fall back to a key.
/// </summary>
public static class EngineeringTagDataSourceAssociation
{
    public static bool IsAssociated(TagEngineeringDto tag, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(dataSource);

        if (tag.DataSourceId.HasValue)
            return dataSource.Id.HasValue && tag.DataSourceId.Value == dataSource.Id.Value;

        return !string.IsNullOrWhiteSpace(tag.Source) &&
               string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Produces a transient planner view. Matching stable identities receive the
    /// current Data Source key so existing protocol planners and diagnostics see
    /// the renamed canonical key. A stable identity belonging elsewhere is
    /// hidden from this planner even if its stale compatibility key happens to
    /// match, preventing silent rebinding after delete/recreate operations.
    /// </summary>
    public static EngineeringPackage NormalizeForPlanner(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        return package with
        {
            Tags = package.Tags.Select(tag => NormalizeTagForPlanner(tag, dataSource)).ToArray()
        };
    }

    public static TagEngineeringDto NormalizeTagForPlanner(
        TagEngineeringDto tag,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!tag.DataSourceId.HasValue)
            return tag;

        return dataSource.Id.HasValue && tag.DataSourceId.Value == dataSource.Id.Value
            ? tag with { Source = dataSource.Key }
            : tag with { Source = null };
    }
}
