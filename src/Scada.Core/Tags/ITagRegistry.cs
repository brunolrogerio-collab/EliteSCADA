namespace Scada.Core.Tags;

public interface ITagRegistry
{
    TagDefinition Register(TagDefinition tag);
    TagDefinition Upsert(TagDefinition tag);
    bool TryGet(Guid tagId, out TagDefinition? tag);
    bool TryGetByPath(string path, out TagDefinition? tag);
    IReadOnlyCollection<TagDefinition> Snapshot();
}
