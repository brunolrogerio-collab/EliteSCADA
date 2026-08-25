namespace Scada.Core.Tags;

public sealed record TagDefinition(
    Guid Id,
    string Name,
    string Path,
    TagDataType DataType,
    string? Source,
    string? EngineeringUnit,
    string? Description,
    bool ReadOnly,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static TagDefinition Create(
        string name,
        string path,
        TagDataType dataType,
        string? source = null,
        string? engineeringUnit = null,
        string? description = null,
        bool readOnly = false,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(Guid.NewGuid(), name, path, dataType, source, engineeringUnit, description, readOnly, metadata);
}
