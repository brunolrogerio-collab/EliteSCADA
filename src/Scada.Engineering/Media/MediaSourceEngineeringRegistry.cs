using Scada.Engineering.Contracts;

namespace Scada.Engineering.Media;

public interface IMediaSourceEngineeringRegistry
{
    IReadOnlyCollection<MediaSourceEngineeringDto> Snapshot();
    MediaSourceEngineeringDto? Find(Guid id);
    MediaSourceEngineeringDto? FindByKey(string key);
    void Upsert(MediaSourceEngineeringDto source);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryMediaSourceEngineeringRegistry(Action? changed = null)
    : IMediaSourceEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, MediaSourceEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MediaSourceEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(source => source.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public MediaSourceEngineeringDto? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public MediaSourceEngineeringDto? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(MediaSourceEngineeringDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var errors = MediaSourceEngineeringValidation.Validate(source);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors.Select(issue => issue.Message)), nameof(source));

        var normalized = source with { Id = source.Id ?? Guid.NewGuid() };
        var id = normalized.Id!.Value;
        lock (_sync)
        {
            if (_byKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                throw new InvalidOperationException($"Media source key '{normalized.Key}' is already assigned to another stable identity.");

            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            _byId[id] = normalized;
            _byKey[normalized.Key] = id;
        }
        changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        MediaSourceEngineeringDto? removed;
        lock (_sync)
        {
            if (!_byId.Remove(id, out removed)) return false;
            _byKey.Remove(removed.Key);
        }
        changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        bool hadSources;
        lock (_sync)
        {
            hadSources = _byId.Count > 0;
            _byId.Clear();
            _byKey.Clear();
        }
        if (hadSources) changed?.Invoke();
    }
}
