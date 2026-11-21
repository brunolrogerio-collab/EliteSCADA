using System.Collections.Concurrent;

namespace Scada.Core.Tags;

public sealed class InMemoryTagRegistry : ITagRegistry
{
    private readonly ConcurrentDictionary<Guid, TagDefinition> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly Action? _changed;
    private IReadOnlyCollection<TagDefinition>? _sortedSnapshot;

    public InMemoryTagRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public TagDefinition Register(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        Validate(tag);
        lock (_gate)
        {
            if (_byPath.TryGetValue(tag.Path, out var existingId) && existingId != tag.Id)
                throw new InvalidOperationException($"A tag with path '{tag.Path}' is already registered.");
            if (_byId.ContainsKey(tag.Id))
                throw new InvalidOperationException($"A tag with id '{tag.Id}' is already registered.");

            _byId[tag.Id] = tag;
            _byPath[tag.Path] = tag.Id;
            _sortedSnapshot = null;
        }
        _changed?.Invoke();
        return tag;
    }

    public TagDefinition Upsert(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        Validate(tag);
        lock (_gate)
        {
            if (_byPath.TryGetValue(tag.Path, out var pathOwner) && pathOwner != tag.Id)
                throw new InvalidOperationException($"A tag with path '{tag.Path}' is already registered.");

            var exists = _byId.TryGetValue(tag.Id, out var previous);
            if (exists && previous is not null && !previous.Path.Equals(tag.Path, StringComparison.OrdinalIgnoreCase))
                _byPath.TryRemove(previous.Path, out _);

            _byId[tag.Id] = tag;
            _byPath[tag.Path] = tag.Id;
            _sortedSnapshot = null;
        }
        _changed?.Invoke();
        return tag;
    }

    public bool Remove(Guid tagId)
    {
        TagDefinition? removed;
        lock (_gate)
        {
            if (!_byId.TryRemove(tagId, out removed)) return false;
            _byPath.TryRemove(removed.Path, out _);
            _sortedSnapshot = null;
        }
        _changed?.Invoke();
        return true;
    }

    public bool TryGet(Guid tagId, out TagDefinition? tag)
    {
        var found = _byId.TryGetValue(tagId, out var value);
        tag = value;
        return found;
    }

    public bool TryGetByPath(string path, out TagDefinition? tag)
    {
        tag = null;
        return _byPath.TryGetValue(path, out var id) && _byId.TryGetValue(id, out tag);
    }

    public IReadOnlyCollection<TagDefinition> Snapshot()
    {
        lock (_gate)
        {
            return _sortedSnapshot ??= Array.AsReadOnly(
                _byId.Values.OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _byId.Clear();
            _byPath.Clear();
            _sortedSnapshot = null;
        }
        _changed?.Invoke();
    }

    private static void Validate(TagDefinition tag)
    {
        if (string.IsNullOrWhiteSpace(tag.Path)) throw new ArgumentException("Tag path is required.", nameof(tag));
        if (string.IsNullOrWhiteSpace(tag.Name)) throw new ArgumentException("Tag name is required.", nameof(tag));
    }
}
