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

public static class VisualNavigationActionVersions
{
    public const int RichCommand = 2;
}

public enum DynamoParameterKind
{
    Boolean,
    Number,
    String,
    EquipmentPath,
    TagReference,
    ValueSource,
    Command
}

public sealed record DynamoParameterDefinitionEngineeringDto(
    string Key,
    DynamoParameterKind Kind,
    bool Required = false,
    JsonElement? DefaultValue = null,
    TagValueReference? DefaultTagReference = null,
    int Version = VisualCompositionEngineeringVersions.Current,
    VisualValueSourceEngineeringDto? DefaultValueSource = null,
    VisualExpressionValueType? ValueSourceType = null);

public sealed record DynamoParameterValueEngineeringDto(
    string Key,
    DynamoParameterKind Kind,
    JsonElement? Value = null,
    TagValueReference? TagReference = null,
    int Version = VisualCompositionEngineeringVersions.Current,
    Guid? CommandId = null,
    VisualValueSourceEngineeringDto? ValueSource = null);

public enum VisualNavigationActionKind
{
    NavigateScreen,
    OpenPopup,
    ClosePopup,
    ExecuteCommand,
    SetTagValue,
    ToggleTagBoolean,
    ExecuteRichCommand
}

/// <summary>
/// Canonical visual action intent. CommandId selects either the legacy Command
/// action or the separately versioned Active Rich Command action. Rich command
/// parameter values are collected at invocation time and never stored here.
/// </summary>
public sealed record VisualNavigationActionEngineeringDto(
    string EventKey,
    VisualNavigationActionKind Kind,
    string? TargetKey = null,
    Dictionary<string, JsonElement>? Parameters = null,
    int Version = VisualCompositionEngineeringVersions.Current,
    Guid? CommandId = null,
    string? CommandParameterKey = null);
