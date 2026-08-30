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
        return issues;
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
        if (string.IsNullOrWhiteSpace(element.DynamoKey))
            issues.Add(Error("VISUAL_DYNAMO_PARAMETERS_REQUIRE_DYNAMO", $"Visual element '{element.Key}' declares Dynamo parameters without a DynamoKey.", kind, entityKey));
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
        bool allowMissing,
        ImportEntityKind kind,
        string entityKey,
        List<ImportIssue> issues,
        string role)
    {
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

            if (action.Kind is VisualNavigationActionKind.NavigateScreen or VisualNavigationActionKind.OpenPopup)
            {
                if (string.IsNullOrWhiteSpace(action.TargetKey))
                    issues.Add(Error("VISUAL_ACTION_TARGET_REQUIRED", $"Navigation action '{action.EventKey}' requires a target key.", kind, entityKey));
            }
            else if (action.Kind == VisualNavigationActionKind.ClosePopup && !string.IsNullOrWhiteSpace(action.TargetKey))
            {
                issues.Add(Error("VISUAL_ACTION_TARGET_NOT_ALLOWED", $"ClosePopup action '{action.EventKey}' cannot declare a target key.", kind, entityKey));
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
            if (!string.IsNullOrWhiteSpace(element.DynamoKey))
                issues.Add(Error("DYNAMO_NESTING_NOT_SUPPORTED", $"Dynamo definition '{entityKey}' cannot nest Dynamo '{element.DynamoKey}' in composition version 1.", ImportEntityKind.Dynamo, entityKey));

            issues.AddRange(ValidateElement(element, ImportEntityKind.Dynamo, entityKey));
            ValidateDefinitionElements(element.Children, entityKey, issues, ids);
        }
    }

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
