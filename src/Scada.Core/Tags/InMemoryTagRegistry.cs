using System.Collections.Concurrent;

namespace Scada.Core.Tags;

public sealed class InMemoryTagRegistry : ITagRegistry
{
    private readonly ConcurrentDictionary<Guid, TagDefinition> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

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
        }
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

            if (_byId.TryGetValue(tag.Id, out var previous) && !previous.Path.Equals(tag.Path, StringComparison.OrdinalIgnoreCase))
                _byPath.TryRemove(previous.Path, out _);

            _byId[tag.Id] = tag;
            _byPath[tag.Path] = tag.Id;
        }
        return tag;
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

    public IReadOnlyCollection<TagDefinition> Snapshot() =>
        _byId.Values.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Clear()
    {
        lock (_gate)
        {
            _byId.Clear();
            _byPath.Clear();
        }
    }

    private static void Validate(TagDefinition tag)
    {
        if (string.IsNullOrWhiteSpace(tag.Path)) throw new ArgumentException("Tag path is required.", nameof(tag));
        if (string.IsNullOrWhiteSpace(tag.Name)) throw new ArgumentException("Tag name is required.", nameof(tag));
    }
}
