namespace Scada.Core.Tags;

public interface ICurrentTagCache
{
    bool TryGet(Guid tagId, out TagValue? value);
    IReadOnlyCollection<TagValue> Snapshot();
    ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default);
}
