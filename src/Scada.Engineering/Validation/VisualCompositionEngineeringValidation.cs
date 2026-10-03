using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

public static class VisualCompositionEngineeringValidation
{
    private const int MaximumParameters = 128;
    private const int MaximumActions = 64;

    public static IReadOnlyCollection<ImportIssue> ValidateDynamo(
        DynamoEngineeringDto dynamo)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(dynamo.Key) ? dynamo.Name : dynamo.Key;
        ValidateParameterDefinitions(dynamo.Parameters, ImportEntityKind.Dynamo, key, issues);
        ValidateDefinitionElements(dynamo.Elements, key, issues, new HashSet<Guid>());
        ValidateParameterizedDynamoStateSources(dynamo.Elements, dynamo.Parameters, key, issues);
        ValidateParameterizedCommandActions(dynamo.Elements, dynamo.Parameters, key, issues);
        ValidateDynamoParameterSources(dynamo.Elements, dynamo.Parameters, key, issues);
        return issues;
    }

    private static void ValidateDynamoParameterSources(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters,
        string entityKey,
        List<ImportIssue> issues)
    {
        var definitions = (parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>())
            .Where(parameter => parameter is not null && !string.IsNullOrWhiteSpace(parameter.Key))
            .ToDictionary(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null) continue;

            foreach (var binding in element.Bindings ?? Array.Empty<EngineeringBindingDto>())
            {
                if (binding is null) continue;
                var metadataKey = binding.Metadata?.TryGetValue("dynamoParameter", out var declaredKey) == true
                    ? declaredKey
                    : null;
                var targetKey = VisualDynamicEngineeringValidation.TryDynamoParameterTarget(binding.Target, out var parsedBindingKey)
                    ? parsedBindingKey
                    : null;
                var parameterKey = !string.IsNullOrWhiteSpace(metadataKey) ? metadataKey : targetKey;
                if (string.IsNullOrWhiteSpace(parameterKey)) continue;

                if (!definitions.ContainsKey(parameterKey))
                {
                    issues.Add(Error(
                        "DYNAMO_DYNAMIC_PARAMETER_NOT_FOUND",
                        $"Dynamo binding '{binding.Key}' on '{element.Key}' references undeclared public parameter '{parameterKey}'.",
                        ImportEntityKind.Dynamo,
                        entityKey));
                }
            }

            foreach (var source in DynamicSources(element))
            {
                if (!VisualDynamicEngineeringValidation.TryDynamoParameterTarget(source.Target, out var parameterKey))
                    continue;

                if (!definitions.TryGetValue(parameterKey, out var parameter))
                {
                    issues.Add(Error(
                        "DYNAMO_DYNAMIC_PARAMETER_NOT_FOUND",
                        $"Dynamo visual source on '{element.Key}' references undeclared public parameter '{parameterKey}'.",
                        ImportEntityKind.Dynamo,
                        entityKey));
                    continue;
                }

                var compatible = source.ValueType switch
                {
                    VisualExpressionValueType.Boolean =>
                        parameter.Kind is DynamoParameterKind.Boolean or DynamoParameterKind.TagReference,
                    VisualExpressionValueType.Number =>
                        parameter.Kind is DynamoParameterKind.Number or DynamoParameterKind.TagReference,
                    _ => false
                };
                if (!compatible)
                {
                    issues.Add(Error(
                        "DYNAMO_DYNAMIC_PARAMETER_TYPE_MISMATCH",
                        $"Dynamo visual source on '{element.Key}' expects {source.ValueType} but public parameter '{parameter.Key}' is {parameter.Kind}.",
                        ImportEntityKind.Dynamo,
                        entityKey));
                }
            }

            ValidateDynamoParameterSources(element.Children, parameters, entityKey, issues);
        }
    }

    private static IEnumerable<VisualValueSourceEngineeringDto> DynamicSources(VisualElementEngineeringDto element)
    {
        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
            if (condition?.Source is not null)
                yield return condition.Source;
        foreach (var map in element.PropertyMaps ?? Array.Empty<VisualPropertyMapEngineeringDto>())
            if (map?.Source is not null)
                yield return map.Source;
        if (element.AnalogFill?.Source is not null)
            yield return element.AnalogFill.Source;
    }

    private static void ValidateParameterizedCommandActions(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters,
        string entityKey,
        List<ImportIssue> issues)
    {
        var declared = (parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>())
            .Where(parameter => parameter is not null && !string.IsNullOrWhiteSpace(parameter.Key))
            .ToDictionary(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null) continue;
            foreach (var action in element.Actions ?? Array.Empty<VisualNavigationActionEngineeringDto>())
            {
                if (action is null || action.Kind != VisualNavigationActionKind.ExecuteCommand ||
                    string.IsNullOrWhiteSpace(action.CommandParameterKey))
                    continue;

                if (action.CommandId.HasValue)
                    issues.Add(Error(
                        "DYNAMO_COMMAND_ACTION_ID_NOT_PORTABLE",
                        $"Dynamo action '{action.EventKey}' must not combine CommandParameterKey with a concrete CommandId.",
                        ImportEntityKind.Dynamo,
                        entityKey));

                if (!declared.TryGetValue(action.CommandParameterKey, out var parameter) ||
                    parameter.Kind != DynamoParameterKind.Command)
                    issues.Add(Error(
                        "DYNAMO_COMMAND_PARAMETER_NOT_FOUND",
                        $"Dynamo action '{action.EventKey}' references Command parameter '{action.CommandParameterKey}', which is not declared as Command.",
                        ImportEntityKind.Dynamo,
                        entityKey));
            }

            ValidateParameterizedCommandActions(element.Children, parameters, entityKey, issues);
        }
    }

    private static void ValidateParameterizedDynamoStateSources(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters,
        string entityKey,
        List<ImportIssue> issues)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null) continue;
            if (element.Metadata?.TryGetValue("dynamoStateColorParameter", out var parameterKey) == true &&
                !string.IsNullOrWhiteSpace(parameterKey) &&
                !(parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>()).Any(parameter =>
                    parameter is not null &&
                    parameter.Kind == DynamoParameterKind.TagReference &&
                    parameter.Key.Equals(parameterKey, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(
                    "DYNAMO_STATE_COLOR_PARAMETER_NOT_FOUND",
                    $"Dynamo state-color source '{parameterKey}' must reference a declared TagReference parameter.",
                    ImportEntityKind.Dynamo,
                    entityKey));
            }

            ValidateParameterizedDynamoStateSources(element.Children, parameters, entityKey, issues);
        }
    }

    public static IReadOnlyCollection<ImportIssue> ValidateElement(
        VisualElementEngineeringDto element,
        ImportEntityKind kind,
        string entityKey)
    {
        var issues = new List<ImportIssue>();
        ValidateInstanceParameters(element, kind, entityKey, issues);
        ValidateActions(element.Actions, kind, entityKey, element.Key, issues);
        return issues;
    }

    private static void ValidateParameterDefinitions(
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? definitions,
        ImportEntityKind kind,
        string entityKey,
        List<ImportIssue> issues)
    {
        if (definitions is null) return;
        if (definitions.Count > MaximumParameters)
            issues.Add(Error("DYNAMO_PARAMETER_LIMIT", $"Dynamo '{entityKey}' exceeds the {MaximumParameters} parameter limit.", kind, entityKey));

        var duplicates = definitions
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                issues.Add(Error("DYNAMO_PARAMETER_NULL", "Dynamo parameter definition cannot be null.", kind, entityKey));
                continue;
            }
            if (definition.Version != VisualCompositionEngineeringVersions.Current)
                issues.Add(Error("VISUAL_COMPOSITION_VERSION_UNSUPPORTED", $"Dynamo parameter '{definition.Key}' uses unsupported version {definition.Version}.", kind, entityKey));
            if (string.IsNullOrWhiteSpace(definition.Key))
                issues.Add(Error("DYNAMO_PARAMETER_KEY_REQUIRED", "Dynamo parameter key is required.", kind, entityKey));
            else if (duplicates.Contains(definition.Key))
                issues.Add(Error("DYNAMO_PARAMETER_DUPLICATE", $"Dynamo parameter '{definition.Key}' appears more than once.", kind, entityKey));

            ValidateParameterPayload(
                definition.Key,
                definition.Kind,
                definition.DefaultValue,
                definition.DefaultTagReference,
                commandId: null,
                allowMissing: true,
                kind,
                entityKey,
                issues,
                "definition");
        }
    }

    private static void ValidateInstanceParameters(
        VisualElementEngineeringDto element,
        ImportEntityKind kind,
        string entityKey,
        List<ImportIssue> issues)
    {
        var values = element.DynamoParameters;
        if (values is null) return;
        if (string.IsNullOrWhiteSpace(element.DynamoKey) && !element.DynamoDefinitionId.HasValue)
            issues.Add(Error("VISUAL_DYNAMO_PARAMETERS_REQUIRE_DYNAMO", $"Visual element '{element.Key}' declares Dynamo parameters without a Dynamo definition reference.", kind, entityKey));
        if (values.Count > MaximumParameters)
            issues.Add(Error("VISUAL_DYNAMO_PARAMETER_LIMIT", $"Visual element '{element.Key}' exceeds the {MaximumParameters} Dynamo parameter limit.", kind, entityKey));

        var duplicates = values
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (value is null)
            {
                issues.Add(Error("VISUAL_DYNAMO_PARAMETER_NULL", $"Visual element '{element.Key}' contains a null Dynamo parameter.", kind, entityKey));
                continue;
            }
            if (value.Version != VisualCompositionEngineeringVersions.Current)
                issues.Add(Error("VISUAL_COMPOSITION_VERSION_UNSUPPORTED", $"Dynamo parameter value '{value.Key}' uses unsupported version {value.Version}.", kind, entityKey));
            if (string.IsNullOrWhiteSpace(value.Key))
                issues.Add(Error("VISUAL_DYNAMO_PARAMETER_KEY_REQUIRED", $"Visual element '{element.Key}' contains a Dynamo parameter without a key.", kind, entityKey));
            else if (duplicates.Contains(value.Key))
                issues.Add(Error("VISUAL_DYNAMO_PARAMETER_DUPLICATE", $"Dynamo parameter value '{value.Key}' appears more than once on visual element '{element.Key}'.", kind, entityKey));

            ValidateParameterPayload(
                value.Key,
                value.Kind,
                value.Value,
                value.TagReference,
                value.CommandId,
                allowMissing: false,
                kind,
                entityKey,
                issues,
                "value");
        }
    }

    private static void ValidateParameterPayload(
        string key,
        DynamoParameterKind parameterKind,
        JsonElement? value,
        Scada.Core.Tags.TagValueReference? tagReference,
        Guid? commandId,
        bool allowMissing,
        ImportEntityKind kind,
        string entityKey,
        List<ImportIssue> issues,
        string role)
    {
        if (parameterKind == DynamoParameterKind.Command)
        {
            if (value.HasValue || tagReference is not null)
                issues.Add(Error("DYNAMO_PARAMETER_SHAPE_INVALID", $"Dynamo parameter {role} '{key}' of kind Command cannot carry a scalar value or TAG reference.", kind, entityKey));
            if (!commandId.HasValue || commandId == Guid.Empty)
            {
                if (!allowMissing)
                    issues.Add(Error("DYNAMO_PARAMETER_COMMAND_REQUIRED", $"Dynamo parameter {role} '{key}' requires a stable Command identity.", kind, entityKey));
                return;
            }
            return;
        }

        if (commandId.HasValue)
            issues.Add(Error("DYNAMO_PARAMETER_SHAPE_INVALID", $"Dynamo parameter {role} '{key}' of kind {parameterKind} cannot carry a Command identity.", kind, entityKey));

        if (parameterKind == DynamoParameterKind.TagReference)
        {
            if (value.HasValue)
                issues.Add(Error("DYNAMO_PARAMETER_SHAPE_INVALID", $"Dynamo parameter {role} '{key}' of kind TagReference cannot carry a scalar value.", kind, entityKey));
            if (tagReference is null)
            {
                if (!allowMissing)
                    issues.Add(Error("DYNAMO_PARAMETER_TAG_REQUIRED", $"Dynamo parameter {role} '{key}' requires a stable TAG reference.", kind, entityKey));
                return;
            }
            if (tagReference.TagId == Guid.Empty)
                issues.Add(Error("DYNAMO_PARAMETER_TAG_ID_INVALID", $"Dynamo parameter {role} '{key}' requires a non-empty TAG identity.", kind, entityKey));
            return;
        }

        if (tagReference is not null)
            issues.Add(Error("DYNAMO_PARAMETER_SHAPE_INVALID", $"Dynamo parameter {role} '{key}' of kind {parameterKind} cannot carry a TAG reference.", kind, entityKey));
        if (!value.HasValue)
        {
            if (!allowMissing)
                issues.Add(Error("DYNAMO_PARAMETER_VALUE_REQUIRED", $"Dynamo parameter {role} '{key}' requires a value.", kind, entityKey));
            return;
        }

        var node = value.Value;
        var valid = parameterKind switch
        {
            DynamoParameterKind.Boolean => node.ValueKind is JsonValueKind.True or JsonValueKind.False,
            DynamoParameterKind.Number => node.ValueKind == JsonValueKind.Number && node.TryGetDouble(out var number) && double.IsFinite(number),
            DynamoParameterKind.String or DynamoParameterKind.EquipmentPath => node.ValueKind == JsonValueKind.String,
            _ => false
        };
        if (!valid)
            issues.Add(Error("DYNAMO_PARAMETER_VALUE_TYPE_INVALID", $"Dynamo parameter {role} '{key}' does not match declared kind {parameterKind}.", kind, entityKey));
    }

    private static void ValidateActions(
        IReadOnlyCollection<VisualNavigationActionEngineeringDto>? actions,
        ImportEntityKind kind,
        string entityKey,
        string elementKey,
        List<ImportIssue> issues)
    {
        if (actions is null) return;
        if (actions.Count > MaximumActions)
            issues.Add(Error("VISUAL_ACTION_LIMIT", $"Visual element '{elementKey}' exceeds the {MaximumActions} navigation action limit.", kind, entityKey));

        var duplicates = actions
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.EventKey))
            .GroupBy(x => x.EventKey, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            if (action is null)
            {
                issues.Add(Error("VISUAL_ACTION_NULL", $"Visual element '{elementKey}' contains a null navigation action.", kind, entityKey));
                continue;
            }
            if (action.Version != VisualCompositionEngineeringVersions.Current)
                issues.Add(Error("VISUAL_COMPOSITION_VERSION_UNSUPPORTED", $"Navigation action '{action.EventKey}' uses unsupported version {action.Version}.", kind, entityKey));
            if (string.IsNullOrWhiteSpace(action.EventKey))
                issues.Add(Error("VISUAL_ACTION_EVENT_REQUIRED", $"Visual element '{elementKey}' navigation action requires an event key.", kind, entityKey));
            else if (duplicates.Contains(action.EventKey))
                issues.Add(Error("VISUAL_ACTION_EVENT_DUPLICATE", $"Visual element '{elementKey}' has more than one navigation action for event '{action.EventKey}'.", kind, entityKey));

            if (action.Kind is VisualNavigationActionKind.NavigateScreen or VisualNavigationActionKind.OpenPopup or
                VisualNavigationActionKind.SetTagValue or VisualNavigationActionKind.ToggleTagBoolean)
            {
                if (string.IsNullOrWhiteSpace(action.TargetKey))
                    issues.Add(Error("VISUAL_ACTION_TARGET_REQUIRED", $"Visual action '{action.EventKey}' requires a target key.", kind, entityKey));
            }
            else if (action.Kind == VisualNavigationActionKind.ClosePopup && !string.IsNullOrWhiteSpace(action.TargetKey))
            {
                issues.Add(Error("VISUAL_ACTION_TARGET_NOT_ALLOWED", $"ClosePopup action '{action.EventKey}' cannot declare a target key.", kind, entityKey));
            }

            if (action.Kind == VisualNavigationActionKind.ExecuteCommand)
            {
                var hasCommandId = action.CommandId.HasValue && action.CommandId != Guid.Empty;
                var hasParameter = !string.IsNullOrWhiteSpace(action.CommandParameterKey);
                if (hasCommandId == hasParameter)
                    issues.Add(Error(
                        "VISUAL_ACTION_COMMAND_REFERENCE_INVALID",
                        $"ExecuteCommand action '{action.EventKey}' requires exactly one CommandId or CommandParameterKey.",
                        kind,
                        entityKey));
            }
            else if (!string.IsNullOrWhiteSpace(action.CommandParameterKey))
            {
                issues.Add(Error(
                    "VISUAL_ACTION_COMMAND_PARAMETER_NOT_ALLOWED",
                    $"Visual action '{action.EventKey}' of kind {action.Kind} cannot carry CommandParameterKey.",
                    kind,
                    entityKey));
            }

            if (action.Kind == VisualNavigationActionKind.SetTagValue &&
                (action.Parameters is not { Count: 1 } || !action.Parameters.TryGetValue("value", out var value) ||
                 value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String)))
            {
                issues.Add(Error("VISUAL_ACTION_TAG_VALUE_REQUIRED", $"SetTagValue action '{action.EventKey}' requires one primitive 'value' parameter.", kind, entityKey));
            }
            if (action.Kind == VisualNavigationActionKind.ToggleTagBoolean && action.Parameters is { Count: > 0 })
            {
                issues.Add(Error("VISUAL_ACTION_TAG_TOGGLE_PARAMETERS_NOT_ALLOWED", $"ToggleTagBoolean action '{action.EventKey}' cannot declare parameters.", kind, entityKey));
            }
        }
    }

    private static void ValidateDefinitionElements(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        string entityKey,
        List<ImportIssue> issues,
        HashSet<Guid> ids)
    {
        if (elements is null) return;
        var duplicateKeys = elements
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            if (element is null)
            {
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_NULL", "Dynamo visual element cannot be null.", ImportEntityKind.Dynamo, entityKey));
                continue;
            }
            if (string.IsNullOrWhiteSpace(element.Key))
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_KEY_REQUIRED", "Dynamo visual element key is required.", ImportEntityKind.Dynamo, entityKey));
            else if (duplicateKeys.Contains(element.Key))
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_DUPLICATE", $"Dynamo visual element key '{element.Key}' appears more than once at the same level.", ImportEntityKind.Dynamo, entityKey));
            if (string.IsNullOrWhiteSpace(element.Type))
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_TYPE_REQUIRED", $"Dynamo visual element '{element.Key}' requires a type.", ImportEntityKind.Dynamo, entityKey));
            if (element.Id == Guid.Empty)
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_ID_EMPTY", $"Dynamo visual element '{element.Key}' cannot use an empty Id.", ImportEntityKind.Dynamo, entityKey));
            else if (element.Id.HasValue && !ids.Add(element.Id.Value))
                issues.Add(Error("DYNAMO_VISUAL_ELEMENT_ID_DUPLICATE", $"Dynamo visual element Id '{element.Id.Value:D}' appears more than once.", ImportEntityKind.Dynamo, entityKey));
            if (!string.IsNullOrWhiteSpace(element.DynamoKey) || element.DynamoDefinitionId.HasValue)
            {
                var nestedReference = !string.IsNullOrWhiteSpace(element.DynamoKey)
                    ? element.DynamoKey
                    : element.DynamoDefinitionId!.Value.ToString("D");
                issues.Add(Error("DYNAMO_NESTING_NOT_SUPPORTED", $"Dynamo definition '{entityKey}' cannot nest Dynamo '{nestedReference}' in composition version 1.", ImportEntityKind.Dynamo, entityKey));
            }

            issues.AddRange(ValidateElement(element, ImportEntityKind.Dynamo, entityKey));
            ValidateDefinitionElements(element.Children, entityKey, issues, ids);
        }
    }

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
