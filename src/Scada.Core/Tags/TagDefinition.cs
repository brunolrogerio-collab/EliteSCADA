namespace Scada.Core.Tags;

public sealed record TagAccessPolicy(
    IReadOnlyCollection<string>? ReadRoles = null,
    IReadOnlyCollection<string>? WriteRoles = null,
    IReadOnlyCollection<string>? ConfigureRoles = null);

/// <summary>
/// Optional, explicit reference to a versioned Engineering unit catalog entry.
/// The free-text EngineeringUnit remains the source/display label.
/// </summary>
public sealed record ControlledEngineeringUnitReference(int CatalogVersion, string Code);

public sealed record TagDefinition(
    Guid Id,
    string Name,
    string Path,
    TagDataType DataType,
    string? Source,
    string? EngineeringUnit,
    string? Description,
    bool ReadOnly,
    IReadOnlyDictionary<string, string>? Metadata = null,
    TagAccessPolicy? AccessPolicy = null,
    TagValueSelector? AddressSelector = null,
    CommunicationTagBinding? CommunicationBinding = null,
    Guid? DataSourceId = null,
    ControlledEngineeringUnitReference? ControlledEngineeringUnit = null)
{
    public static TagDefinition Create(
        string name,
        string path,
        TagDataType dataType,
        string? source = null,
        string? engineeringUnit = null,
        string? description = null,
        bool readOnly = false,
        IReadOnlyDictionary<string, string>? metadata = null,
        TagAccessPolicy? accessPolicy = null,
        TagValueSelector? addressSelector = null,
        CommunicationTagBinding? communicationBinding = null,
        Guid? dataSourceId = null,
        ControlledEngineeringUnitReference? controlledEngineeringUnit = null)
        => new(
            Guid.NewGuid(),
            name,
            path,
            dataType,
            source,
            engineeringUnit,
            description,
            readOnly,
            metadata,
            accessPolicy,
            addressSelector,
            communicationBinding,
            dataSourceId,
            controlledEngineeringUnit);
}
