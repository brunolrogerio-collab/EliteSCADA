using System.Security.Cryptography;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualAssets;

public sealed record VisualAssetPayload(
    string Sha256,
    string MediaType,
    byte[] Content)
{
    public long ByteLength => Content.LongLength;

    public VisualAssetPayload Clone() => this with { Content = Content.ToArray() };

    public static VisualAssetPayload Create(string mediaType, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        var bytes = content.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new VisualAssetPayload(hash, mediaType, bytes);
    }
}

public sealed record EngineeringImportContext(
    IReadOnlyDictionary<string, VisualAssetPayload>? VisualAssetPayloads = null)
{
    public static EngineeringImportContext Empty { get; } = new();

    public bool TryGetVisualAssetPayload(string sha256, out VisualAssetPayload payload)
    {
        if (VisualAssetPayloads is not null &&
            VisualAssetPayloads.TryGetValue(sha256, out var found))
        {
            payload = found;
            return true;
        }

        payload = null!;
        return false;
    }
}

public interface IVisualAssetEngineeringRegistry
{
    IReadOnlyCollection<VisualAssetEngineeringDto> SnapshotAssets();
    VisualAssetEngineeringDto? FindAsset(Guid id);
    VisualAssetEngineeringDto? FindAssetByKey(string key);
    void UpsertAsset(VisualAssetEngineeringDto asset);
    bool RemoveAsset(Guid id);
    void Clear();

    bool HasPayload(string sha256);
    VisualAssetPayload? FindPayload(string sha256);
    void PutPayload(VisualAssetPayload payload);
    IReadOnlyDictionary<string, VisualAssetPayload> SnapshotPayloads(IEnumerable<string>? hashes = null);
}

public sealed class InMemoryVisualAssetEngineeringRegistry : IVisualAssetEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, VisualAssetEngineeringDto> _assetsById = new();
    private readonly Dictionary<string, Guid> _assetIdsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VisualAssetPayload> _payloadsByHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryVisualAssetEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<VisualAssetEngineeringDto> SnapshotAssets()
    {
        lock (_sync)
            return _assetsById.Values
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public VisualAssetEngineeringDto? FindAsset(Guid id)
    {
        lock (_sync)
            return _assetsById.GetValueOrDefault(id);
    }

    public VisualAssetEngineeringDto? FindAssetByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _assetIdsByKey.TryGetValue(key, out var id)
                ? _assetsById.GetValueOrDefault(id)
                : null;
    }

    public void UpsertAsset(VisualAssetEngineeringDto asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Key);

        var normalized = asset with { Id = asset.Id ?? Guid.NewGuid() };
        var id = normalized.Id!.Value;
        if (id == Guid.Empty)
            throw new ArgumentException("Visual asset ID cannot be empty.", nameof(asset));

        lock (_sync)
        {
            if (_assetsById.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _assetIdsByKey.Remove(previous.Key);

            if (_assetIdsByKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _assetsById.Remove(otherId);

            _assetsById[id] = normalized;
            _assetIdsByKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public bool RemoveAsset(Guid id)
    {
        bool removed;
        lock (_sync)
        {
            removed = _assetsById.Remove(id, out var previous);
            if (removed && previous is not null)
                _assetIdsByKey.Remove(previous.Key);
        }

        if (removed) _changed?.Invoke();
        return removed;
    }

    public void Clear()
    {
        bool changed;
        lock (_sync)
        {
            changed = _assetsById.Count != 0 || _payloadsByHash.Count != 0;
            _assetsById.Clear();
            _assetIdsByKey.Clear();
            _payloadsByHash.Clear();
        }

        if (changed) _changed?.Invoke();
    }

    public bool HasPayload(string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return false;
        lock (_sync)
            return _payloadsByHash.ContainsKey(sha256);
    }

    public VisualAssetPayload? FindPayload(string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return null;
        lock (_sync)
            return _payloadsByHash.TryGetValue(sha256, out var payload)
                ? payload.Clone()
                : null;
    }

    public void PutPayload(VisualAssetPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var actualHash = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
        if (!actualHash.Equals(payload.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Visual asset payload SHA-256 does not match its declared hash.");

        lock (_sync)
        {
            if (_payloadsByHash.TryGetValue(actualHash, out var existing) &&
                !existing.Content.AsSpan().SequenceEqual(payload.Content))
                throw new InvalidDataException("Visual asset hash collision or inconsistent payload detected.");

            _payloadsByHash[actualHash] = payload with
            {
                Sha256 = actualHash,
                Content = payload.Content.ToArray()
            };
        }
    }

    public IReadOnlyDictionary<string, VisualAssetPayload> SnapshotPayloads(IEnumerable<string>? hashes = null)
    {
        lock (_sync)
        {
            IEnumerable<KeyValuePair<string, VisualAssetPayload>> selected = _payloadsByHash;
            if (hashes is not null)
            {
                var wanted = hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                selected = selected.Where(x => wanted.Contains(x.Key));
            }

            return selected.ToDictionary(
                x => x.Key,
                x => x.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public void ReplaceAssets(
        IEnumerable<VisualAssetEngineeringDto> assets,
        IEnumerable<VisualAssetPayload> payloads)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(payloads);

        var normalizedAssets = assets.Select(asset =>
        {
            if (!asset.Id.HasValue || asset.Id.Value == Guid.Empty)
                throw new InvalidDataException("A restored visual asset requires a stable non-empty ID.");
            return asset;
        }).ToArray();
        var duplicateIds = normalizedAssets.GroupBy(x => x.Id!.Value).FirstOrDefault(x => x.Count() > 1);
        if (duplicateIds is not null)
            throw new InvalidDataException($"Duplicate visual asset ID '{duplicateIds.Key}'.");
        var duplicateKeys = normalizedAssets
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateKeys is not null)
            throw new InvalidDataException($"Duplicate visual asset key '{duplicateKeys.Key}'.");

        var normalizedPayloads = payloads.ToDictionary(
            x => x.Sha256,
            x => x.Clone(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var asset in normalizedAssets)
        {
            if (!normalizedPayloads.TryGetValue(asset.Sha256, out var payload))
                throw new InvalidDataException($"Visual asset '{asset.Key}' payload '{asset.Sha256}' is missing.");
            var actualHash = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
            if (!actualHash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase) ||
                payload.ByteLength != asset.ByteLength)
                throw new InvalidDataException($"Visual asset '{asset.Key}' payload integrity check failed.");
        }

        lock (_sync)
        {
            _assetsById.Clear();
            _assetIdsByKey.Clear();
            _payloadsByHash.Clear();

            foreach (var asset in normalizedAssets)
            {
                _assetsById[asset.Id!.Value] = asset;
                _assetIdsByKey[asset.Key] = asset.Id.Value;
            }

            foreach (var (hash, payload) in normalizedPayloads)
                _payloadsByHash[hash] = payload;
        }

        _changed?.Invoke();
    }
}
