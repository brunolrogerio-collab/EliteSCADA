using System.Text.Json;
using Scada.Core.Tags;

namespace Scada.Engineering.Contracts;

/// <summary>
/// Versioned public Engineering contract for Wave 09 Popup/Dynamo composition
/// and deterministic navigation. Runtime/renderer state is deliberately excluded.
/// </summary>
public static class VisualCompositionEngineeringVersions
{
    public const int Current = 1;
}

public enum DynamoParameterKind
{
    Boolean,
    Number,
    String,
    EquipmentPath,
    TagReference
}

public sealed record DynamoParameterDefinitionEngineeringDto(
    string Key,
    DynamoParameterKind Kind,
    bool Required = false,
    JsonElement? DefaultValue = null,
    TagValueReference? DefaultTagReference = null,
    int Version = VisualCompositionEngineeringVersions.Current);

public sealed record DynamoParameterValueEngineeringDto(
    string Key,
    DynamoParameterKind Kind,
    JsonElement? Value = null,
    TagValueReference? TagReference = null,
    int Version = VisualCompositionEngineeringVersions.Current);

public enum VisualNavigationActionKind
{
    NavigateScreen,
    OpenPopup,
    ClosePopup,
    ExecuteCommand
}

/// <summary>
/// Canonical visual action intent. TargetKey is Engineering identity by key for
/// navigation targets; CommandId is the stable Command entity identity for
/// ExecuteCommand. Parameters are JSON-native authoring values passed to the
/// target context and are not runtime-calculated state.
/// </summary>
public sealed record VisualNavigationActionEngineeringDto(
    string EventKey,
    VisualNavigationActionKind Kind,
    string? TargetKey = null,
    Dictionary<string, JsonElement>? Parameters = null,
    int Version = VisualCompositionEngineeringVersions.Current,
    Guid? CommandId = null);
