using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Structural/type validation for FOLLOW-B canonical visual behavior. This class
/// deliberately does not parse or evaluate expression text; Runtime owns the
/// bounded expression language. Engineering validates public shape, destination
/// compatibility and deterministic dependency declarations.
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
        var occupiedProperties = new HashSet<string>(StringComparer.Ordinal);

        foreach (var binding in element.Bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding is not null && !string.IsNullOrWhiteSpace(binding.Key))
                occupiedProperties.Add(binding.Key);
        }

        var expressionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var propertyExpression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
        {
            if (propertyExpression is null)
            {
                issues.Add(Error("VISUAL_EXPRESSION_NULL", "Visual property expression cannot be null.", entityKind, entityKey));
                continue;
            }

            ValidateVersion(propertyExpression.Version, "property expression", entityKind, entityKey, issues);
            var propertyKey = propertyExpression.PropertyKey;
            if (string.IsNullOrWhiteSpace(propertyKey))
            {
                issues.Add(Error("VISUAL_EXPRESSION_PROPERTY_REQUIRED", "Visual property expression requires a destination property.", entityKind, entityKey));
                continue;
            }

            if (!expressionKeys.Add(propertyKey))
                issues.Add(Error("VISUAL_EXPRESSION_PROPERTY_DUPLICATE", $"Visual property '{propertyKey}' has more than one expression.", entityKind, entityKey));
            if (occupiedProperties.Contains(propertyKey))
                issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_SOURCE_CONFLICT", $"Visual property '{propertyKey}' already has another Binding/Expression source.", entityKind, entityKey));

            ValidateDestination(schema, propertyKey, propertyExpression.Expression?.ResultType, "expression", entityKind, entityKey, issues);
            ValidateExpression(propertyExpression.Expression, entityKind, entityKey, issues);
            occupiedProperties.Add(propertyKey);
        }

        var conditionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
        {
            if (condition is null)
            {
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_NULL", "Visual Boolean Condition cannot be null.", entityKind, entityKey));
                continue;
            }

            ValidateVersion(condition.Version, "Boolean Condition", entityKind, entityKey, issues);
            if (string.IsNullOrWhiteSpace(condition.PropertyKey))
            {
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_PROPERTY_REQUIRED", "Visual Boolean Condition requires a destination property.", entityKind, entityKey));
                continue;
            }

            if (!conditionKeys.Add(condition.PropertyKey))
                issues.Add(Error("VISUAL_BOOLEAN_CONDITION_PROPERTY_DUPLICATE", $"Visual property '{condition.PropertyKey}' has more than one Boolean Condition.", entityKind, entityKey));
            if (occupiedProperties.Contains(condition.PropertyKey))
                issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_SOURCE_CONFLICT", $"Visual property '{condition.PropertyKey}' already has another Binding/Expression source.", entityKind, entityKey));

            ValidateBooleanDestination(schema, condition.PropertyKey, entityKind, entityKey, issues);
            ValidateBooleanCondition(condition, entityKind, entityKey, issues);
            occupiedProperties.Add(condition.PropertyKey);
        }

        if (element.AnalogFill is not null)
            ValidateAnalogFill(element.AnalogFill, schema, entityKind, entityKey, issues);

        return issues;
    }

    private static void ValidateDestination(
        VisualObjectPropertySchema schema,
        string propertyKey,
        VisualExpressionValueType? resultType,
        string sourceName,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (!schema.Declares(propertyKey))
        {
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_UNKNOWN", $"Visual {sourceName} targets undeclared property '{propertyKey}'.", entityKind, entityKey));
            return;
        }

        var property = schema.GetRequired(propertyKey);
        if (!property.SupportsBinding)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_NOT_BINDABLE", $"Visual property '{propertyKey}' does not support Binding/Expression sources.", entityKind, entityKey));

        var expected = ToExpressionType(property.ValueKind);
        if (expected is null)
        {
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_TYPE_UNSUPPORTED", $"Visual property '{propertyKey}' does not support typed visual expressions in FOLLOW-B.", entityKind, entityKey));
            return;
        }

        if (resultType.HasValue && resultType.Value != expected.Value)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_TYPE_MISMATCH", $"Visual property '{propertyKey}' requires a {expected.Value} result but the source declares {resultType.Value}.", entityKind, entityKey));
    }

    private static void ValidateBooleanDestination(
        VisualObjectPropertySchema schema,
        string propertyKey,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (!schema.Declares(propertyKey))
        {
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_UNKNOWN", $"Boolean Condition targets undeclared property '{propertyKey}'.", entityKind, entityKey));
            return;
        }

        var property = schema.GetRequired(propertyKey);
        if (!property.SupportsBinding)
            issues.Add(Error("VISUAL_DYNAMIC_PROPERTY_NOT_BINDABLE", $"Visual property '{propertyKey}' does not support Binding/Expression sources.", entityKind, entityKey));
        if (property.ValueKind != VisualPropertyValueKind.Boolean)
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_DESTINATION_INVALID", $"Boolean Condition destination '{propertyKey}' must be a Boolean visual property.", entityKind, entityKey));
    }

    private static void ValidateBooleanCondition(
        VisualBooleanConditionEngineeringDto condition,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (!Enum.IsDefined(condition.Kind))
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_KIND_INVALID", $"Boolean Condition uses unsupported kind '{condition.Kind}'.", entityKind, entityKey));
            return;
        }
        if (!Enum.IsDefined(condition.IntervalMode))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_INTERVAL_MODE_INVALID", $"Boolean Condition uses unsupported interval mode '{condition.IntervalMode}'.", entityKind, entityKey));

        ValidateValueSource(condition.Source, entityKind, entityKey, issues);

        switch (condition.Kind)
        {
            case VisualBooleanConditionKind.Direct:
                if (condition.Source is not null && condition.Source.ValueType != VisualExpressionValueType.Boolean)
                    issues.Add(Error("VISUAL_BOOLEAN_CONDITION_SOURCE_TYPE_INVALID", "Direct Boolean Condition requires a Boolean source.", entityKind, entityKey));
                if (condition.Minimum.HasValue || condition.Maximum.HasValue)
                    issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUNDS_UNEXPECTED", "Direct Boolean Condition cannot declare numeric interval bounds.", entityKind, entityKey));
                break;

            case VisualBooleanConditionKind.NumericInterval:
                if (condition.Source is not null && condition.Source.ValueType != VisualExpressionValueType.Number)
                    issues.Add(Error("VISUAL_BOOLEAN_CONDITION_SOURCE_TYPE_INVALID", "Numeric interval Boolean Condition requires a numeric source.", entityKind, entityKey));
                if (condition.Negate)
                    issues.Add(Error("VISUAL_BOOLEAN_CONDITION_NEGATE_INVALID", "Numeric interval uses Inside/Outside mode instead of a separate negate flag.", entityKind, entityKey));
                ValidateInterval(condition, entityKind, entityKey, issues);
                break;
        }
    }

    private static void ValidateInterval(
        VisualBooleanConditionEngineeringDto condition,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (!condition.Minimum.HasValue && !condition.Maximum.HasValue)
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_REQUIRED", "Numeric interval requires at least one bound.", entityKind, entityKey));
            return;
        }

        if (condition.Minimum.HasValue && !double.IsFinite(condition.Minimum.Value))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_INVALID", "Numeric interval minimum must be finite.", entityKind, entityKey));
        if (condition.Maximum.HasValue && !double.IsFinite(condition.Maximum.Value))
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_BOUND_INVALID", "Numeric interval maximum must be finite.", entityKind, entityKey));

        if (!condition.Minimum.HasValue || !condition.Maximum.HasValue ||
            !double.IsFinite(condition.Minimum.Value) || !double.IsFinite(condition.Maximum.Value))
            return;

        if (condition.Minimum.Value > condition.Maximum.Value ||
            (condition.Minimum.Value == condition.Maximum.Value && (!condition.MinimumInclusive || !condition.MaximumInclusive)))
        {
            issues.Add(Error("VISUAL_BOOLEAN_CONDITION_RANGE_INVALID", "Numeric interval bounds must describe a non-empty deterministic range.", entityKind, entityKey));
        }
    }

    private static void ValidateAnalogFill(
        VisualAnalogFillEngineeringDto fill,
        VisualObjectPropertySchema schema,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        ValidateVersion(fill.Version, "Analog Fill", entityKind, entityKey, issues);
        if (!schema.SupportsAnalogFill)
            issues.Add(Error("VISUAL_ANALOG_FILL_NOT_SUPPORTED", $"Visual object type '{schema.ObjectTypeKey}' does not declare Analog Fill capability.", entityKind, entityKey));

        ValidateValueSource(fill.Source, entityKind, entityKey, issues);
        if (fill.Source is not null && fill.Source.ValueType != VisualExpressionValueType.Number)
            issues.Add(Error("VISUAL_ANALOG_FILL_SOURCE_TYPE_INVALID", "Analog Fill requires a numeric source.", entityKind, entityKey));

        if (!double.IsFinite(fill.InputMinimum) || !double.IsFinite(fill.InputMaximum) || fill.InputMinimum >= fill.InputMaximum)
            issues.Add(Error("VISUAL_ANALOG_FILL_SCALE_INVALID", "Analog Fill input minimum and maximum must be finite and minimum must be less than maximum.", entityKind, entityKey));
        if (!IsStableHexColor(fill.FillColor))
            issues.Add(Error("VISUAL_ANALOG_FILL_COLOR_INVALID", "Analog Fill color must use #RRGGBB or #RRGGBBAA.", entityKind, entityKey));
        if (!Enum.IsDefined(fill.Direction))
            issues.Add(Error("VISUAL_ANALOG_FILL_DIRECTION_INVALID", $"Analog Fill uses unsupported direction '{fill.Direction}'.", entityKind, entityKey));
    }

    private static void ValidateValueSource(
        VisualValueSourceEngineeringDto? source,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (source is null)
        {
            issues.Add(Error("VISUAL_VALUE_SOURCE_REQUIRED", "Visual dynamic behavior requires a value source.", entityKind, entityKey));
            return;
        }

        ValidateVersion(source.Version, "value source", entityKind, entityKey, issues);
        if (!Enum.IsDefined(source.Kind))
        {
            issues.Add(Error("VISUAL_VALUE_SOURCE_KIND_INVALID", $"Visual value source uses unsupported kind '{source.Kind}'.", entityKind, entityKey));
            return;
        }
        if (!Enum.IsDefined(source.ValueType))
            issues.Add(Error("VISUAL_VALUE_SOURCE_TYPE_INVALID", $"Visual value source uses unsupported value type '{source.ValueType}'.", entityKind, entityKey));

        switch (source.Kind)
        {
            case VisualValueSourceKind.Tag:
            case VisualValueSourceKind.ClientMemory:
                if (source.TagReference is null)
                    issues.Add(Error("VISUAL_VALUE_SOURCE_REFERENCE_REQUIRED", $"{source.Kind} source requires a stable TagReference.", entityKind, entityKey));
                if (source.Expression is not null)
                    issues.Add(Error("VISUAL_VALUE_SOURCE_EXPRESSION_UNEXPECTED", $"{source.Kind} source cannot also contain an expression.", entityKind, entityKey));
                break;

            case VisualValueSourceKind.Expression:
                if (source.TagReference is not null)
                    issues.Add(Error("VISUAL_VALUE_SOURCE_REFERENCE_UNEXPECTED", "Expression source cannot also contain a direct TagReference.", entityKind, entityKey));
                ValidateExpression(source.Expression, entityKind, entityKey, issues);
                if (source.Expression is not null && source.Expression.ResultType != source.ValueType)
                    issues.Add(Error("VISUAL_VALUE_SOURCE_EXPRESSION_TYPE_MISMATCH", "Expression source type must match its expression result type.", entityKind, entityKey));
                break;
        }
    }

    private static void ValidateExpression(
        VisualExpressionEngineeringDto? expression,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (expression is null)
        {
            issues.Add(Error("VISUAL_EXPRESSION_REQUIRED", "Visual expression configuration is required.", entityKind, entityKey));
            return;
        }

        ValidateVersion(expression.Version, "expression", entityKind, entityKey, issues);
        if (!Enum.IsDefined(expression.ResultType))
            issues.Add(Error("VISUAL_EXPRESSION_RESULT_TYPE_INVALID", $"Visual expression uses unsupported result type '{expression.ResultType}'.", entityKind, entityKey));
        if (string.IsNullOrWhiteSpace(expression.Text))
            issues.Add(Error("VISUAL_EXPRESSION_TEXT_REQUIRED", "Visual expression text is required.", entityKind, entityKey));
        else if (expression.Text.Length > MaximumExpressionLength)
            issues.Add(Error("VISUAL_EXPRESSION_TEXT_LIMIT", $"Visual expression text exceeds the {MaximumExpressionLength} character Engineering limit.", entityKind, entityKey));

        var dependencies = expression.Dependencies ?? Array.Empty<VisualExpressionDependencyEngineeringDto>();
        if (dependencies.Count > MaximumDependencies)
            issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_LIMIT", $"Visual expression exceeds the {MaximumDependencies} dependency Engineering limit.", entityKind, entityKey));

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in dependencies)
        {
            if (dependency is null)
            {
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_NULL", "Visual expression dependency cannot be null.", entityKind, entityKey));
                continue;
            }

            ValidateVersion(dependency.Version, "expression dependency", entityKind, entityKey, issues);
            if (string.IsNullOrWhiteSpace(dependency.Symbol))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_SYMBOL_REQUIRED", "Visual expression dependency requires a symbol.", entityKind, entityKey));
            else if (!symbols.Add(dependency.Symbol))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_SYMBOL_DUPLICATE", $"Visual expression dependency symbol '{dependency.Symbol}' is duplicated.", entityKind, entityKey));
            if (!Enum.IsDefined(dependency.Kind))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_KIND_INVALID", $"Visual expression dependency uses unsupported kind '{dependency.Kind}'.", entityKind, entityKey));
            if (!Enum.IsDefined(dependency.ValueType))
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_TYPE_INVALID", $"Visual expression dependency uses unsupported value type '{dependency.ValueType}'.", entityKind, entityKey));
            if (dependency.TagReference is null || dependency.TagReference.TagId == Guid.Empty)
                issues.Add(Error("VISUAL_EXPRESSION_DEPENDENCY_REFERENCE_REQUIRED", $"Visual expression dependency '{dependency.Symbol}' requires a stable non-empty TagReference.", entityKind, entityKey));
        }
    }

    private static VisualExpressionValueType? ToExpressionType(VisualPropertyValueKind kind) => kind switch
    {
        VisualPropertyValueKind.Boolean => VisualExpressionValueType.Boolean,
        VisualPropertyValueKind.Number or VisualPropertyValueKind.Integer => VisualExpressionValueType.Number,
        _ => null
    };

    private static void ValidateVersion(
        int version,
        string configuration,
        ImportEntityKind entityKind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (version != VisualDynamicEngineeringVersions.Current)
            issues.Add(Error("VISUAL_DYNAMIC_VERSION_UNSUPPORTED", $"Visual {configuration} version {version} is unsupported; expected {VisualDynamicEngineeringVersions.Current}.", entityKind, entityKey));
    }

    private static bool IsStableHexColor(string? value)
    {
        if (value is null || value.Length is not (7 or 9) || value[0] != '#')
            return false;
        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
                return false;
        }
        return true;
    }

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
