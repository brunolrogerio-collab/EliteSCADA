using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Structural/type validation for FOLLOW-B canonical visual behavior. Expression
/// parsing/evaluation remains Runtime ownership; Engineering validates persisted
/// intent, destination compatibility and deterministic dependency declarations.
/// </summary>
public static class VisualDynamicEngineeringValidation
{
    private const int MaximumExpressionLength = 4096;
    private const int MaximumDependencies = 128;

    public static IReadOnlyCollection<ImportIssue> Validate(
        VisualElementEngineeringDto element,
        VisualObjectPropertySchema schema,
        ImportEntityKind entityKind,
        string entityKey)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(schema);

        var issues = new List<ImportIssue>();
        var occupied = (element.Bindings ?? Array.Empty<EngineeringBindingDto>())
            .Where(binding => binding is not null && !string.IsNullOrWhiteSpace(binding.Key))
            .Select(binding => binding.Key)
            .ToHashSet(StringComparer.Ordinal);

        ValidatePropertyExpressions(element.PropertyExpressions, schema, occupied, entityKind, entityKey, issues);
        ValidateBooleanConditions(element.BooleanConditions, schema, occupied, entityKind, entityKey, issues);

        if (element.AnalogFill is not null)
            ValidateAnalogFill(element.AnalogFill, schema, entityKind, entityKey, issues);

        return issues;
    }

    private static void ValidatePropertyExpressions(
        IReadOnlyCollection<VisualPropertyExpressionEngineeringDto>? expressions,
        VisualObjectPropertySchema schema,
        HashSet<string> occupied,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in expressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
        {
            if (item is null)
            {
                issues.Add(Error("VISUAL_EXPRESSION_NULL", "Visual property expression cannot be null.", kind, key));
                continue;
            }

            Version(item.Version, "property expression", kind, key, issues);
            if (string.IsNullOrWhiteSpace(item.PropertyKey))
            {
                issues.Add(Error("VISUAL_EXPRESSION_PROPERTY_REQUIRED", "Visual property expression requires a destination property.", kind, key));
                continue;
            }

            if (!seen.Add(item.PropertyKey))
                issues.Add(Error("VISUAL_EXPRESSION_PROPERTY_DUPLICATE", $"Visual property '{item.PropertyKey}' has more than one expression.", kind, key));
            if (occupied.Contains(item.PropertyKey))
                issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_SOURCE_CONFLICT", $"Visual property '{item.PropertyKey}' already has another Binding/Expression source.", kind, key));

            ValidateDestination(schema, item.PropertyKey, item.Expression?.ResultType, kind, key, issues);
            ValidateExpression(item.Expression, kind, key, issues);
            occupied.Add(item.PropertyKey);
        }
    }

    private static void ValidateBooleanConditions(
        IReadOnlyCollection<VisualBooleanConditionEngineeringDto>? conditions,
        VisualObjectPropertySchema schema,
        HashSet<string> occupied,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var condition in conditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
        {
            if (condition is null)
            {
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_NULL", "Visual Boolean Condition cannot be null.", kind, key));
                continue;
            }

            Version(condition.Version, "Boolean Condition", kind, key, issues);
            if (string.IsNullOrWhiteSpace(condition.PropertyKey))
            {
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_PROPERTY_REQUIRED", "Visual Boolean Condition requires a destination property.", kind, key));
                continue;
            }

            if (!seen.Add(condition.PropertyKey))
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_PROPERTY_DUPLICATE", $"Visual property '{condition.PropertyKey}' has more than one Boolean Condition.", kind, key));
            if (occupied.Contains(condition.PropertyKey))
                issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_SOURCE_CONFLICT", $"Visual property '{condition.PropertyKey}' already has another Binding/Expression source.", kind, key));

            ValidateBooleanDestination(schema, condition.PropertyKey, kind, key, issues);
            ValidateBooleanCondition(condition, kind, key, issues);
            occupied.Add(condition.PropertyKey);
        }
    }

    private static void ValidateDestination(
        VisualObjectPropertySchema schema,
        string propertyKey,
        VisualExpressionValueType? resultType,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (!schema.Declares(propertyKey))
        {
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_UNKNOWN", $"Visual expression targets undeclared property '{propertyKey}'.", kind, key));
            return;
        }

        var property = schema.GetRequired(propertyKey);
        if (!property.SupportsBinding)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_NOT_BINDABLE", $"Visual property '{propertyKey}' does not support Binding/Expression sources.", kind, key));

        var expected = PropertyType(property.ValueKind);
        if (!expected.HasValue)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_TYPE_UNSUPPORTED", $"Visual property '{propertyKey}' is not Boolean/numeric and cannot receive a FOLLOW-B typed expression.", kind, key));
        else if (resultType.HasValue && resultType.Value != expected.Value)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_TYPE_MISMATCH", $"Visual property '{propertyKey}' requires {expected.Value} but the source declares {resultType.Value}.", kind, key));
    }

    private static void ValidateBooleanDestination(
        VisualObjectPropertySchema schema,
        string propertyKey,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (!schema.Declares(propertyKey))
        {
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_UNKNOWN", $"Boolean Condition targets undeclared property '{propertyKey}'.", kind, key));
            return;
        }

        var property = schema.GetRequired(propertyKey);
        if (!property.SupportsBinding)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_NOT_BINDABLE", $"Visual property '{propertyKey}' does not support Binding/Expression sources.", kind, key));
        if (property.ValueKind != VisualPropertyValueKind.Boolean)
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_DESTINATION_INVALID", $"Boolean Condition destination '{propertyKey}' must be Boolean.", kind, key));
    }

    private static void ValidateBooleanCondition(
        VisualBooleanConditionEngineeringDto condition,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (!Enum.IsDefined(condition.Kind))
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_KIND_INVALID", $"Unsupported Boolean Condition kind '{condition.Kind}'.", kind, key));
            return;
        }
        if (!Enum.IsDefined(condition.IntervalMode))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_INTERVAL_MODE_INVALID", $"Unsupported interval mode '{condition.IntervalMode}'.", kind, key));

        ValidateSource(condition.Source, kind, key, issues);
        if (condition.Kind == VisualBooleanConditionKind.Direct)
        {
            if (condition.Source is not null && condition.Source.ValueType != VisualExpressionValueType.Boolean)
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_SOURCE_TYPE_INVALID", "Direct Boolean Condition requires a Boolean source.", kind, key));
            if (condition.Minimum.HasValue || condition.Maximum.HasValue)
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUNDS_UNEXPECTED", "Direct Boolean Condition cannot declare interval bounds.", kind, key));
            return;
        }

        if (condition.Source is not null && condition.Source.ValueType != VisualExpressionValueType.Number)
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_SOURCE_TYPE_INVALID", "Numeric interval Boolean Condition requires a numeric source.", kind, key));
        if (condition.Negate)
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_NEGATE_INVALID", "Numeric interval uses Inside/Outside mode instead of Negate.", kind, key));
        ValidateInterval(condition, kind, key, issues);
    }

    private static void ValidateInterval(
        VisualBooleanConditionEngineeringDto condition,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (!condition.Minimum.HasValue && !condition.Maximum.HasValue)
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_REQUIRED", "Numeric interval requires at least one bound.", kind, key));
            return;
        }

        if (condition.Minimum.HasValue && !double.IsFinite(condition.Minimum.Value))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_INVALID", "Numeric interval minimum must be finite.", kind, key));
        if (condition.Maximum.HasValue && !double.IsFinite(condition.Maximum.Value))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_INVALID", "Numeric interval maximum must be finite.", kind, key));

        if (condition.Minimum.HasValue && condition.Maximum.HasValue &&
            double.IsFinite(condition.Minimum.Value) && double.IsFinite(condition.Maximum.Value) &&
            (condition.Minimum.Value > condition.Maximum.Value ||
             (condition.Minimum.Value == condition.Maximum.Value && (!condition.MinimumInclusive || !condition.MaximumInclusive))))
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_RANGE_INVALID", "Numeric interval bounds must describe a non-empty deterministic range.", kind, key));
        }
    }

    private static void ValidateAnalogFill(
        VisualAnalogFillEngineeringDto fill,
        VisualObjectPropertySchema schema,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        Version(fill.Version, "Analog Fill", kind, key, issues);
        if (!BuiltinVisualObjectSchemas.SupportsAnalogFill(schema.ObjectTypeKey))
            issues.Add(Error("VISUAL_ANALOG_FILL_NOT_SUPPORTED", $"Visual object type '{schema.ObjectTypeKey}' does not declare Analog Fill capability.", kind, key));

        ValidateSource(fill.Source, kind, key, issues);
        if (fill.Source is not null && fill.Source.ValueType != VisualExpressionValueType.Number)
            issues.Add(Error("VISUAL_ANALOG_FILL_SOURCE_TYPE_INVALID", "Analog Fill requires a numeric source.", kind, key));
        if (!double.IsFinite(fill.InputMinimum) || !double.IsFinite(fill.InputMaximum) || fill.InputMinimum >= fill.InputMaximum)
            issues.Add(Error("VISUAL_ANALOG_FILL_SCALE_INVALID", "Analog Fill requires finite inputMinimum < inputMaximum; reverse fill is explicit.", kind, key));
        if (!HexColor(fill.FillColor))
            issues.Add(Error("VISUAL_ANALOG_FILL_COLOR_INVALID", "Analog Fill color must use #RRGGBB or #RRGGBBAA.", kind, key));
        if (!Enum.IsDefined(fill.Direction))
            issues.Add(Error("VISUAL_ANALOG_FILL_DIRECTION_INVALID", $"Unsupported Analog Fill direction '{fill.Direction}'.", kind, key));
    }

    private static void ValidateSource(
        VisualValueSourceEngineeringDto? source,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (source is null)
        {
            issues.Add(Error("VISUAL_VALUE_SOURCE_REQUIRED", "Visual dynamic behavior requires a value source.", kind, key));
            return;
        }

        Version(source.Version, "value source", kind, key, issues);
        if (!Enum.IsDefined(source.Kind))
        {
            issues.Add(Error("VISUAL_VALUE_SOURCE_KIND_INVALID", $"Unsupported visual value source kind '{source.Kind}'.", kind, key));
            return;
        }
        if (!Enum.IsDefined(source.ValueType))
            issues.Add(Error("VISUAL_VALUE_SOURCE_TYPE_INVALID", $"Unsupported visual value source type '{source.ValueType}'.", kind, key));

        if (source.Kind is VisualValueSourceKind.Tag or VisualValueSourceKind.ClientMemory)
        {
            if (source.TagReference is null)
                issues.Add(Error("VISUAL_VALUE_SOURCE_REFERENCE_REQUIRED", $"{source.Kind} source requires a stable TagReference.", kind, key));
            if (source.Expression is not null)
                issues.Add(Error("VISUAL_VALUE_SOURCE_EXPRESSION_UNEXPECTED", $"{source.Kind} source cannot also contain an expression.", kind, key));
            return;
        }

        if (source.TagReference is not null)
            issues.Add(Error("VISUAL_VALUE_SOURCE_REFERENCE_UNEXPECTED", "Expression source cannot also contain a direct TagReference.", kind, key));
        ValidateExpression(source.Expression, kind, key, issues);
        if (source.Expression is not null && source.Expression.ResultType != source.ValueType)
            issues.Add(Error("VISUAL_VALUE_SOURCE_EXPRESSION_TYPE_MISMATCH", "Expression source type must match its expression result type.", kind, key));
    }

    private static void ValidateExpression(
        VisualExpressionEngineeringDto? expression,
        ImportEntityKind kind,
        string key,
        List<ImportIssue> issues)
    {
        if (expression is null)
        {
            issues.Add(Error("VISUAL_EXPRESSION_REQUIRED", "Visual expression configuration is required.", kind, key));
            return;
        }

        Version(expression.Version, "expression", kind, key, issues);
        if (!Enum.IsDefined(expression.ResultType))
            issues.Add(Error("VISUAL_EXPRESSION_RESULT_TYPE_INVALID", $"Unsupported expression result type '{expression.ResultType}'.", kind, key));
        if (string.IsNullOrWhiteSpace(expression.Text))
            issues.Add(Error("VISUAL_EXPRESSION_TEXT_REQUIRED", "Visual expression text is required.", kind, key));
        else if (expression.Text.Length > MaximumExpressionLength)
            issues.Add(Error("VISUAL_EXPRESSION_TEXT_LIMIT", $"Visual expression exceeds the {MaximumExpressionLength} character Engineering limit.", kind, key));

        var dependencies = expression.Dependencies ?? Array.Empty<VisualExpressionDependencyEngineeringDto>();
        if (dependencies.Count > MaximumDependencies)
            issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_LIMIT", $"Visual expression exceeds the {MaximumDependencies} dependency Engineering limit.", kind, key));

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in dependencies)
        {
            if (dependency is null)
            {
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_NULL", "Visual expression dependency cannot be null.", kind, key));
                continue;
            }

            Version(dependency.Version, "expression dependency", kind, key, issues);
            if (string.IsNullOrWhiteSpace(dependency.Symbol))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_SYMBOL_REQUIRED", "Visual expression dependency requires a symbol.", kind, key));
            else if (!symbols.Add(dependency.Symbol))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_SYMBOL_DUPLICATE", $"Dependency symbol '{dependency.Symbol}' is duplicated.", kind, key));
            if (!Enum.IsDefined(dependency.Kind))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_KIND_INVALID", $"Unsupported dependency kind '{dependency.Kind}'.", kind, key));
            if (!Enum.IsDefined(dependency.ValueType))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_TYPE_INVALID", $"Unsupported dependency type '{dependency.ValueType}'.", kind, key));
            if (dependency.TagReference is null || dependency.TagReference.TagId == Guid.Empty)
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_REFERENCE_REQUIRED", $"Dependency '{dependency.Symbol}' requires a stable non-empty TagReference.", kind, key));
        }
    }

    private static VisualExpressionValueType? PropertyType(VisualPropertyValueKind kind) => kind switch
    {
        VisualPropertyValueKind.Boolean => VisualExpressionValueType.Boolean,
        VisualPropertyValueKind.Number or VisualPropertyValueKind.Integer => VisualExpressionValueType.Number,
        _ => null
    };

    private static void Version(int version, string name, ImportEntityKind kind, string key, List<ImportIssue> issues)
    {
        if (version != VisualDynamicEngineeringVersions.Current)
            issues.Add(Error("VISUAL_DYNAMIC_VERSION_UNSUPPORTED", $"Visual {name} version {version} is unsupported; expected {VisualDynamicEngineeringVersions.Current}.", kind, key));
    }

    private static bool HexColor(string? value) =>
        value is { Length: 7 or 9 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
