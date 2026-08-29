using Scada.Core.Tags;

namespace Scada.Engineering.Contracts;

/// <summary>
/// Versioned public Engineering contract for Wave 08 FOLLOW-B visual behavior.
/// Expression runtime state and calculated results are deliberately excluded.
/// </summary>
public static class VisualDynamicEngineeringVersions
{
    public const int Current = 1;
}

public enum VisualExpressionValueType
{
    Boolean,
    Number
}

public enum VisualExpressionDependencyKind
{
    Tag,
    ClientMemory
}

public enum VisualValueSourceKind
{
    Tag,
    ClientMemory,
    Expression
}

/// <summary>
/// A deterministic expression dependency. Symbol is the token used by the
/// expression text for diagnostics/authoring; TagReference is canonical identity.
/// Friendly path/display text is optional and never becomes identity.
/// </summary>
public sealed record VisualExpressionDependencyEngineeringDto(
    string Symbol,
    VisualExpressionDependencyKind Kind,
    VisualExpressionValueType ValueType,
    TagValueReference TagReference,
    string? Target = null,
    int Version = VisualDynamicEngineeringVersions.Current);

/// <summary>
/// Side-effect-free typed expression source. Parsing/evaluation belongs to the
/// bounded Runtime expression engine; Engineering persists intent and dependencies.
/// </summary>
public sealed record VisualExpressionEngineeringDto(
    string Text,
    VisualExpressionValueType ResultType,
    IReadOnlyCollection<VisualExpressionDependencyEngineeringDto>? Dependencies = null,
    int Version = VisualDynamicEngineeringVersions.Current);

/// <summary>
/// Canonical source used by structured visual behaviors. Direct TAG and Client
/// Memory sources retain stable TagId + optional selector. Expression sources
/// retain their own deterministic dependency set.
/// </summary>
public sealed record VisualValueSourceEngineeringDto(
    VisualValueSourceKind Kind,
    VisualExpressionValueType ValueType,
    string? Target = null,
    TagValueReference? TagReference = null,
    VisualExpressionEngineeringDto? Expression = null,
    int Version = VisualDynamicEngineeringVersions.Current);

public sealed record VisualPropertyExpressionEngineeringDto(
    string PropertyKey,
    VisualExpressionEngineeringDto Expression,
    int Version = VisualDynamicEngineeringVersions.Current);

public enum VisualBooleanConditionKind
{
    Direct,
    NumericInterval
}

public enum VisualNumericIntervalMode
{
    Inside,
    Outside
}

/// <summary>
/// Structured convenience authoring over the normal Binding/Expression layer.
/// Direct drives a Boolean property from a Boolean source. NumericInterval maps a
/// numeric source to Boolean using explicit bounds and inside/outside semantics.
/// </summary>
public sealed record VisualBooleanConditionEngineeringDto(
    string PropertyKey,
    VisualBooleanConditionKind Kind,
    VisualValueSourceEngineeringDto Source,
    bool Negate = false,
    double? Minimum = null,
    bool MinimumInclusive = true,
    double? Maximum = null,
    bool MaximumInclusive = true,
    VisualNumericIntervalMode IntervalMode = VisualNumericIntervalMode.Inside,
    int Version = VisualDynamicEngineeringVersions.Current);

public enum VisualAnalogFillDirection
{
    BottomToTop,
    TopToBottom,
    LeftToRight,
    RightToLeft
}

/// <summary>
/// Canonical Analog Fill configuration. The effective percentage is Runtime
/// presentation state and is never persisted back into Engineering.
/// </summary>
public sealed record VisualAnalogFillEngineeringDto(
    VisualValueSourceEngineeringDto Source,
    double InputMinimum,
    double InputMaximum,
    string FillColor,
    bool Clamp = true,
    bool InvertScale = false,
    VisualAnalogFillDirection Direction = VisualAnalogFillDirection.BottomToTop,
    int Version = VisualDynamicEngineeringVersions.Current);
