using System.Security.Cryptography;

namespace Scada.Engineering.Libraries;

public sealed record ReusableLibraryCatalogEntry(
    Guid LibraryId,
    string Name,
    string Version,
    string ContentSha256,
    int ResourceCount,
    long ByteLength,
    ReusableLibraryInspection Inspection,
    byte[] Content)
{
    public ReusableLibraryCatalogEntry Copy() => this with { Content = Content.ToArray() };
}

public sealed record ReusableLibraryCatalogDescriptor(
    Guid LibraryId,
    string Name,
    string Version,
    string ContentSha256,
    int ResourceCount,
    long ByteLength);

public sealed record ReusableLibraryAssociationResult(
    ReusableLibraryCatalogDescriptor Library,
    bool Added);

public sealed class ReusableLibraryAssociationConflictException : InvalidOperationException
{
    public ReusableLibraryAssociationConflictException(Guid libraryId)
        : base($"Reusable library '{libraryId:D}' is already associated with different content. Disassociate it before attaching another version.")
    {
        LibraryId = libraryId;
    }

    public Guid LibraryId { get; }
}

/// <summary>
/// Process-local Engineering catalog for associated .escadalib content.
/// This is deliberately not canonical project/Runtime authority: no source path is stored,
/// association does not mutate Working, and the catalog can disappear on process restart
/// without invalidating project-owned content already incorporated through later flows.
/// </summary>
public sealed class ReusableLibraryCatalogStore
{
    public const int MaximumAssociatedLibraries = 32;
    public const long MaximumCatalogBytes = 256L * 1024 * 1024;

    private readonly object _sync = new();
    private readonly Dictionary<Guid, ReusableLibraryCatalogEntry> _byId = new();

    public static ReusableLibraryCatalogStore Shared { get; } = new();

    public IReadOnlyCollection<ReusableLibraryCatalogDescriptor> Snapshot()
    {
        lock (_sync)
        {
            return _byId.Values
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.LibraryId)
                .Select(ToDescriptor)
                .ToArray();
        }
    }

    public ReusableLibraryCatalogEntry? Find(Guid libraryId)
    {
        if (libraryId == Guid.Empty) return null;
        lock (_sync)
            return _byId.TryGetValue(libraryId, out var entry) ? entry.Copy() : null;
    }

    public ReusableLibraryAssociationResult Associate(
        ReadOnlyMemory<byte> libraryBytes,
        ReusableLibraryInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (libraryBytes.IsEmpty)
            throw new InvalidDataException("Reusable library is empty.");
        if (libraryBytes.Length > ReusableLibraryPackageService.MaximumPackageBytes)
            throw new InvalidDataException("Reusable library exceeds its package safety limit.");

        var manifest = inspection.Manifest;
        if (manifest.LibraryId == Guid.Empty)
            throw new InvalidDataException("Reusable library requires a stable non-empty library ID.");

        var bytes = libraryBytes.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var entry = new ReusableLibraryCatalogEntry(
            manifest.LibraryId,
            manifest.Name,
            manifest.Version,
            hash,
            manifest.Resources.Count,
            bytes.LongLength,
            inspection,
            bytes);

        lock (_sync)
        {
            if (_byId.TryGetValue(manifest.LibraryId, out var existing))
            {
                if (!existing.ContentSha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
                    throw new ReusableLibraryAssociationConflictException(manifest.LibraryId);

                return new ReusableLibraryAssociationResult(ToDescriptor(existing), Added: false);
            }

            if (_byId.Count >= MaximumAssociatedLibraries)
                throw new InvalidDataException("Reusable library catalog contains too many associated libraries.");

            long projectedBytes;
            try
            {
                projectedBytes = checked(_byId.Values.Sum(x => x.ByteLength) + bytes.LongLength);
            }
            catch (OverflowException ex)
            {
                throw new InvalidDataException("Reusable library catalog exceeds its memory safety limit.", ex);
            }

            if (projectedBytes > MaximumCatalogBytes)
                throw new InvalidDataException("Reusable library catalog exceeds its memory safety limit.");

            _byId.Add(manifest.LibraryId, entry);
            return new ReusableLibraryAssociationResult(ToDescriptor(entry), Added: true);
        }
    }

    public bool Disassociate(Guid libraryId)
    {
        if (libraryId == Guid.Empty) return false;
        lock (_sync)
            return _byId.Remove(libraryId);
    }

    internal void Clear()
    {
        lock (_sync)
            _byId.Clear();
    }

    private static ReusableLibraryCatalogDescriptor ToDescriptor(ReusableLibraryCatalogEntry entry) =>
        new(
            entry.LibraryId,
            entry.Name,
            entry.Version,
            entry.ContentSha256,
            entry.ResourceCount,
            entry.ByteLength);
}
