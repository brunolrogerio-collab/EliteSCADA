using System.Collections.Concurrent;
using Scada.Core.Abstractions;
using Scada.Core.Events;

namespace Scada.Core.Tags;

public sealed class CurrentTagCache(IScadaEventBus eventBus) : ICurrentTagCache
{
    private readonly ConcurrentDictionary<Guid, TagValue> _values = new();

    public bool TryGet(Guid tagId, out TagValue? value)
    {
        var found = _values.TryGetValue(tagId, out var current);
        value = current;
        return found;
    }

    public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();

    public async ValueTask<TagValue?> UpdateAsync(
        TagDefinition tag,
        TagValue value,
        CancellationToken cancellationToken = default)
    {
        if (tag.Id != value.TagId)
            throw new ArgumentException("TagDefinition.Id must match TagValue.TagId.", nameof(value));

        _values.TryGetValue(tag.Id, out var previous);
        _values[tag.Id] = value;

        await eventBus.PublishAsync(
            new TagValueChanged(tag, previous, value, DateTimeOffset.UtcNow),
            cancellationToken);

        return previous;
    }
}
