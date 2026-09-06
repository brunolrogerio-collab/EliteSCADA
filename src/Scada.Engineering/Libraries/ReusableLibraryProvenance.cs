namespace Scada.Engineering.Libraries;

/// <summary>
/// Informational origin metadata attached to project-owned resources incorporated
/// from a reusable library. These values are provenance only: they never contain a
/// source path or become Runtime/library resolution authority.
/// </summary>
public static class ReusableLibraryProvenance
{
    public const string MetadataPrefix = "elitescada.reusable.origin.";
    public const string LibraryIdKey = MetadataPrefix + "libraryId";
    public const string LibraryVersionKey = MetadataPrefix + "libraryVersion";
    public const string ResourceIdKey = MetadataPrefix + "resourceId";
    public const string ResourceKindKey = MetadataPrefix + "resourceKind";
    public const string PayloadSha256Key = MetadataPrefix + "payloadSha256";

    public static Dictionary<string, string> Stamp(
        IReadOnlyDictionary<string, string>? metadata,
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(resource);

        if (manifest.LibraryId == Guid.Empty)
            throw new InvalidDataException("Reusable resource provenance requires a stable library ID.");
        if (resource.ResourceId == Guid.Empty)
            throw new InvalidDataException("Reusable resource provenance requires a stable resource ID.");
        if (string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Reusable resource provenance requires a library version.");
        if (string.IsNullOrWhiteSpace(resource.Kind))
            throw new InvalidDataException("Reusable resource provenance requires a resource kind.");

        var payload = manifest.Files.SingleOrDefault(file =>
            file.Path.Equals(resource.PayloadPath, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' provenance payload is missing from the manifest.");

        var result = WithoutOrigin(metadata)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        result[LibraryIdKey] = manifest.LibraryId.ToString("D");
        result[LibraryVersionKey] = manifest.Version;
        result[ResourceIdKey] = resource.ResourceId.ToString("D");
        result[ResourceKindKey] = resource.Kind;
        result[PayloadSha256Key] = payload.Sha256.ToLowerInvariant();
        return result;
    }

    public static Dictionary<string, string>? WithoutOrigin(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            if (!IsOriginKey(pair.Key))
                result[pair.Key] = pair.Value;
        }

        return result.Count == 0 ? null : result;
    }

    public static bool IsOriginKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase);
}
