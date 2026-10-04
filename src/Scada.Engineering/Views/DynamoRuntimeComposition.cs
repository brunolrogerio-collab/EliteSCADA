using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Views;

/// <summary>
/// Canonical Dynamo composition view for Runtime consumers. The instance and
/// definition element identities remain separate, so repeated instances never
/// rewrite canonical child IDs or create renderer-owned identity.
/// </summary>
public sealed record DynamoRuntimeComposition(
    Guid InstanceId,
    Guid DefinitionId,
    string DefinitionKey,
    IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> Parameters,
    IReadOnlyCollection<VisualElementEngineeringDto> Elements);

public static class DynamoRuntimeComposer
{
    public static DynamoRuntimeComposition Compose(
        VisualElementEngineeringDto instance,
        DynamoEngineeringDto definition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);

        if (instance.DynamoDefinitionId.HasValue)
        {
            if (!definition.Id.HasValue || definition.Id.Value == Guid.Empty ||
                instance.DynamoDefinitionId.Value != definition.Id.Value)
                throw new ArgumentException("Visual element stable Dynamo reference does not match the supplied definition.", nameof(instance));
            if (!string.IsNullOrWhiteSpace(instance.DynamoKey) &&
                !instance.DynamoKey.Equals(definition.Key, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Visual element Dynamo alias does not match the supplied definition.", nameof(instance));
        }
        else if (string.IsNullOrWhiteSpace(instance.DynamoKey) ||
                 !instance.DynamoKey.Equals(definition.Key, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Visual element does not reference the supplied Dynamo definition.", nameof(instance));
        }
        if (!instance.Id.HasValue || instance.Id.Value == Guid.Empty)
            throw new ArgumentException("Dynamo instance requires a stable visual element Id.", nameof(instance));
        if (!definition.Id.HasValue || definition.Id.Value == Guid.Empty)
            throw new ArgumentException("Dynamo definition requires a stable Id.", nameof(definition));

        var supplied = (instance.DynamoParameters ?? Array.Empty<DynamoParameterValueEngineeringDto>())
            .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, DynamoParameterValueEngineeringDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in definition.Parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>())
        {
            if (supplied.TryGetValue(parameter.Key, out var value))
            {
                // Earlier catalog revisions stored a direct TAG as TagReference.
                // A typed ValueSource is the forward-compatible representation,
                // but accept the old payload and project it as a direct TAG read.
                // Do not mutate the persisted instance; the editor can migrate it
                // on the next explicit save.
                if (parameter.Kind == DynamoParameterKind.ValueSource &&
                    value.Kind == DynamoParameterKind.TagReference &&
                    value.TagReference is not null &&
                    value.TagReference.TagId != Guid.Empty)
                {
                    value = new DynamoParameterValueEngineeringDto(
                        parameter.Key,
                        DynamoParameterKind.ValueSource,
                        ValueSource: new VisualValueSourceEngineeringDto(
                            VisualValueSourceKind.Tag,
                            parameter.ValueSourceType ?? VisualExpressionValueType.Number,
                            TagReference: value.TagReference));
                }
                if (parameter.Kind == DynamoParameterKind.ValueSource && parameter.ValueSourceType.HasValue &&
                    value.ValueSource?.ValueType != parameter.ValueSourceType.Value)
                    throw new InvalidOperationException(
                        $"Dynamo parameter '{parameter.Key}' requires a {parameter.ValueSourceType.Value} visual value source.");
                resolved[parameter.Key] = value;
                continue;
            }

            if (parameter.Kind == DynamoParameterKind.TagReference && parameter.DefaultTagReference is not null)
            {
                resolved[parameter.Key] = new DynamoParameterValueEngineeringDto(
                    parameter.Key,
                    parameter.Kind,
                    TagReference: parameter.DefaultTagReference);
                continue;
            }

            if (parameter.Kind == DynamoParameterKind.ValueSource && parameter.DefaultValueSource is not null)
            {
                if (parameter.ValueSourceType.HasValue && parameter.DefaultValueSource.ValueType != parameter.ValueSourceType.Value)
                    throw new InvalidOperationException(
                        $"Dynamo parameter '{parameter.Key}' default requires a {parameter.ValueSourceType.Value} visual value source.");
                resolved[parameter.Key] = new DynamoParameterValueEngineeringDto(
                    parameter.Key,
                    parameter.Kind,
                    ValueSource: parameter.DefaultValueSource);
                continue;
            }

            if (parameter.DefaultValue.HasValue)
            {
                resolved[parameter.Key] = new DynamoParameterValueEngineeringDto(
                    parameter.Key,
                    parameter.Kind,
                    Value: parameter.DefaultValue);
                continue;
            }

            if (parameter.Required)
                throw new InvalidOperationException($"Required Dynamo parameter '{parameter.Key}' was not supplied.");
        }

        foreach (var extra in supplied.Keys.Where(key => !resolved.ContainsKey(key)))
            throw new InvalidOperationException($"Dynamo instance supplies unknown parameter '{extra}'.");

        return new DynamoRuntimeComposition(
            instance.Id.Value,
            definition.Id.Value,
            definition.Key,
            resolved,
            SubstituteInstanceContext(
                definition.Elements ?? Array.Empty<VisualElementEngineeringDto>(),
                instance.EquipmentPath,
                resolved));
    }

    private static IReadOnlyCollection<VisualElementEngineeringDto> SubstituteInstanceContext(
        IReadOnlyCollection<VisualElementEngineeringDto> elements,
        string? equipmentPath,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        var normalizedPath = equipmentPath?.Trim();

        // Preserve the canonical definition element collection when this instance
        // has no context that can alter the projection. Besides avoiding needless
        // allocations, callers historically rely on this identity to distinguish
        // an untouched canonical definition from an instance-specific projection.
        if (normalizedPath is null && !RequiresInstanceProjection(elements))
            return elements;

        return elements.Select(element => element with
        {
            Properties = ApplyDynamoTextParameter(ApplyFixedDynamoState(element, parameters), element, parameters),
            Bindings = element.Bindings?.Select(binding => binding with
            {
                Target = normalizedPath is null ? binding.Target : binding.Target.Replace("{equipmentPath}", normalizedPath, StringComparison.Ordinal)
            }).ToArray(),
            BooleanConditions = IsDynamoAnimationEnabled(element, parameters)
                ? element.BooleanConditions?.Select(condition => condition with
                {
                    Source = ProjectValueSource(condition.Source, normalizedPath, parameters)
                }).ToArray()
                : null,
            Actions = element.Actions?.Select(action => ProjectAction(action, parameters)).ToArray(),
            PropertyMaps = IsDynamoAnimationEnabled(element, parameters) ? element.PropertyMaps?.Select(map =>
            {
                var stateParameterKey = element.Metadata?.GetValueOrDefault("dynamoStateColorParameter");
                var stateParameter = stateParameterKey is not null && parameters.TryGetValue(stateParameterKey, out var stateValue)
                    ? stateValue
                    : null;
                var profile = element.Metadata?.GetValueOrDefault("dynamoStateColorProfile")?.Split(',') ?? Array.Empty<string>();
                var rules = map.Rules.Select((rule, index) =>
                {
                    var colorParameterKey = index < profile.Length ? $"{profile[index].Trim()}Color" : string.Empty;
                    if (!parameters.TryGetValue(colorParameterKey, out var colorValue) ||
                        colorValue.Kind != DynamoParameterKind.String || !colorValue.Value.HasValue ||
                        colorValue.Value.Value.ValueKind != JsonValueKind.String)
                        return rule;
                    var color = colorValue.Value.Value.GetString();
                    return color is not null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")
                        ? rule with { Value = JsonSerializer.SerializeToElement(color) }
                        : rule;
                }).ToArray();
                var source = ProjectValueSource(map.Source, normalizedPath, parameters);
                if (stateParameter?.Kind == DynamoParameterKind.TagReference && stateParameter.TagReference is not null)
                    source = source with { Target = null, TagReference = stateParameter.TagReference };
                else if (stateParameter?.Kind == DynamoParameterKind.ValueSource && stateParameter.ValueSource is not null && stateParameterKey is not null)
                {
                    var stateSource = ProjectValueSource(source with
                    {
                        ValueType = stateParameter.ValueSource.ValueType,
                        Target = $"{{dynamoParameter:{stateParameterKey}}}",
                        TagReference = null
                    }, normalizedPath, parameters);
                    if (stateSource.ValueType == VisualExpressionValueType.Boolean && source.ValueType == VisualExpressionValueType.Number)
                    {
                        var invertParameterKey = element.Metadata?.GetValueOrDefault("dynamoBooleanStateInvertParameter");
                        var invert = invertParameterKey is not null && parameters.TryGetValue(invertParameterKey, out var invertValue) &&
                            invertValue.Kind == DynamoParameterKind.Boolean && invertValue.Value is { ValueKind: JsonValueKind.True };
                        stateSource = ConvertBooleanStateSourceToNumber(stateSource, invert);
                    }
                    source = stateSource;
                }
                return map with { Source = source, Rules = rules };
            }).ToArray() : null,
            Children = SubstituteInstanceContext(
                element.Children ?? Array.Empty<VisualElementEngineeringDto>(),
                normalizedPath,
                parameters)
        }).ToArray();
    }

    private static Dictionary<string, JsonElement>? ApplyFixedDynamoState(
        VisualElementEngineeringDto element,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        if (element.Properties is null)
            return element.Properties;
        var animationEnabled = IsDynamoAnimationEnabled(element, parameters);
        var properties = new Dictionary<string, JsonElement>(element.Properties, StringComparer.Ordinal);
        if (!animationEnabled && element.Metadata?.TryGetValue("dynamoStateLabelIndex", out var labelIndexText) == true &&
            int.TryParse(labelIndexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var labelIndex) &&
            parameters.TryGetValue("fixedState", out var fixedState) && fixedState.Kind == DynamoParameterKind.Number &&
            fixedState.Value is { ValueKind: JsonValueKind.Number } fixedStateValue)
        {
            properties["visible"] = JsonSerializer.SerializeToElement(fixedStateValue.GetInt32() == labelIndex);
        }
        if (animationEnabled)
            return properties;
        var profile = element.Metadata?.GetValueOrDefault("dynamoStateColorProfile")?.Split(',') ?? Array.Empty<string>();
        var stateParameterKey = element.Metadata?.GetValueOrDefault("dynamoFixedStateParameter");
        if (string.IsNullOrWhiteSpace(stateParameterKey) || !parameters.TryGetValue(stateParameterKey, out var state) ||
            state.Kind != DynamoParameterKind.Number || state.Value is not { ValueKind: JsonValueKind.Number } stateValue)
            return properties;
        var index = stateValue.GetInt32();
        if (element.Metadata?.TryGetValue("dynamoFixedStateProperty", out var fixedProperty) == true &&
            element.Metadata.TryGetValue("dynamoFixedStatePropertyValues", out var fixedValues) &&
            index >= 0 && index < fixedValues.Split(',').Length)
        {
            var selectedValue = fixedValues.Split(',')[index].Trim();
            if (double.TryParse(selectedValue, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedValue) && double.IsFinite(parsedValue))
                properties[fixedProperty] = JsonSerializer.SerializeToElement(parsedValue);
        }
        if (index < 0 || index >= profile.Length) return properties;
        var colorKey = $"{profile[index].Trim()}Color";
        if (!parameters.TryGetValue(colorKey, out var color) || color.Kind != DynamoParameterKind.String ||
            color.Value is not { ValueKind: JsonValueKind.String } colorValue || string.IsNullOrWhiteSpace(colorValue.GetString()))
            return properties;
        if (element.Type == "core.svgSymbol")
            properties["svgPaintOverrides"] = JsonSerializer.SerializeToElement(new
            {
                version = 1, palette = new { }, slots = new { state = new { fill = colorValue.GetString() } }
            });
        else
            properties["fillColor"] = JsonSerializer.SerializeToElement(colorValue.GetString());
        return properties;
    }

    private static Dictionary<string, JsonElement>? ApplyDynamoTextParameter(
        Dictionary<string, JsonElement>? properties,
        VisualElementEngineeringDto element,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        if (properties is null)
            return properties;

        if (element.Type == "core.text" && properties.TryGetValue("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var token = text.GetString();
            if (TryParameterToken(token, out var key) && parameters.TryGetValue(key, out var parameter) &&
                parameter.Kind == DynamoParameterKind.String && parameter.Value is { ValueKind: JsonValueKind.String } value)
                properties["text"] = value.Clone();
        }

        if (element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true &&
            parameters.TryGetValue("labelPosition", out var placement) && placement.Kind == DynamoParameterKind.String &&
            placement.Value is { ValueKind: JsonValueKind.String } positionValue)
        {
            var (x, y, width, height) = positionValue.GetString()?.Trim().ToLowerInvariant() switch
            {
                "above" => (0d, 0d, 132d, 16d),
                "on" => (0d, 42d, 132d, 16d),
                "left" => (0d, 42d, 36d, 16d),
                "right" => (96d, 42d, 36d, 16d),
                _ => (0d, 82d, 132d, 16d)
            };
            properties["x"] = JsonSerializer.SerializeToElement(x);
            properties["y"] = JsonSerializer.SerializeToElement(y);
            properties["width"] = JsonSerializer.SerializeToElement(width);
            properties["height"] = JsonSerializer.SerializeToElement(height);
        }

        if (element.Metadata?.TryGetValue("dynamoOutlineColorParameter", out var outlineParameterKey) == true &&
            parameters.TryGetValue(outlineParameterKey, out var outlineColor) && outlineColor.Kind == DynamoParameterKind.String &&
            outlineColor.Value is { ValueKind: JsonValueKind.String } outlineColorValue &&
            !string.IsNullOrWhiteSpace(outlineColorValue.GetString()))
        {
            if (element.Type == "core.svgSymbol")
            {
                var slots = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                if (properties.TryGetValue("svgPaintOverrides", out var paintOverrides) &&
                    paintOverrides.ValueKind == JsonValueKind.Object && paintOverrides.TryGetProperty("slots", out var existingSlots) &&
                    existingSlots.ValueKind == JsonValueKind.Object)
                {
                    foreach (var slot in existingSlots.EnumerateObject())
                        slots[slot.Name] = slot.Value.Clone();
                }
                foreach (var slotName in new[] { "bezel", "state" })
                {
                    var slotProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    if (slots.TryGetValue(slotName, out var existingSlot) && existingSlot.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in existingSlot.EnumerateObject())
                            slotProperties[property.Name] = property.Value.Clone();
                    }
                    slotProperties["stroke"] = outlineColorValue.Clone();
                    slots[slotName] = JsonSerializer.SerializeToElement(slotProperties);
                }
                properties["svgPaintOverrides"] = JsonSerializer.SerializeToElement(new { version = 1, palette = new { }, slots });
            }
            else
            {
                properties["strokeColor"] = outlineColorValue.Clone();
            }
        }

        if (element.Metadata?.TryGetValue("dynamo3dEffectParameter", out var depthParameterKey) == true &&
            parameters.TryGetValue(depthParameterKey, out var depthEffect) && depthEffect.Kind == DynamoParameterKind.Boolean &&
            depthEffect.Value is { ValueKind: JsonValueKind.True } )
        {
            properties["shadowEnabled"] = JsonSerializer.SerializeToElement(true);
            properties["shadowColor"] = JsonSerializer.SerializeToElement("#24374699");
            properties["shadowOffsetX"] = JsonSerializer.SerializeToElement(1d);
            properties["shadowOffsetY"] = JsonSerializer.SerializeToElement(2d);
            properties["shadowBlur"] = JsonSerializer.SerializeToElement(2d);
        }
        return properties;
    }

    private static bool IsDynamoAnimationEnabled(
        VisualElementEngineeringDto element,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        var parameterKey = element.Metadata?.GetValueOrDefault("dynamoAnimationEnabledParameter");
        return string.IsNullOrWhiteSpace(parameterKey) ||
            !parameters.TryGetValue(parameterKey, out var enabled) ||
            enabled.Kind != DynamoParameterKind.Boolean ||
            enabled.Value is not { ValueKind: JsonValueKind.False };
    }

    private static VisualValueSourceEngineeringDto ProjectValueSource(
        VisualValueSourceEngineeringDto source,
        string? equipmentPath,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        var target = source.Target;
        if (target is not null && equipmentPath is not null)
            target = target.Replace("{equipmentPath}", equipmentPath, StringComparison.Ordinal);
        if (Scada.Engineering.VisualScripting.VisualDynamicEngineeringValidation.TryDynamoParameterTarget(target, out var parameterKey) &&
            parameters.TryGetValue(parameterKey, out var parameter))
        {
            if (parameter.Kind == DynamoParameterKind.TagReference && parameter.TagReference is not null)
                return source with { Target = null, TagReference = parameter.TagReference };
            if (parameter.Kind == DynamoParameterKind.ValueSource && parameter.ValueSource is not null)
            {
                if (parameter.ValueSource.ValueType != source.ValueType)
                    throw new InvalidOperationException(
                        $"Dynamo value-source parameter '{parameter.Key}' produces {parameter.ValueSource.ValueType}, but the visual behavior requires {source.ValueType}.");
                return parameter.ValueSource with
                {
                    Target = parameter.ValueSource.Target is not null && equipmentPath is not null
                        ? parameter.ValueSource.Target.Replace("{equipmentPath}", equipmentPath, StringComparison.Ordinal)
                        : parameter.ValueSource.Target,
                    Expression = parameter.ValueSource.Expression is null ? null : parameter.ValueSource.Expression with
                    {
                        Dependencies = parameter.ValueSource.Expression.Dependencies?.Select(dependency => dependency with
                        {
                            Target = dependency.Target is not null && equipmentPath is not null
                                ? dependency.Target.Replace("{equipmentPath}", equipmentPath, StringComparison.Ordinal)
                                : dependency.Target
                        }).ToArray()
                    }
                };
            }
        }
        return source with { Target = target };
    }

    private static VisualValueSourceEngineeringDto ConvertBooleanStateSourceToNumber(
        VisualValueSourceEngineeringDto source,
        bool invert)
    {
        if (source.ValueType != VisualExpressionValueType.Boolean)
            return source;

        var expressionText = source.Kind == VisualValueSourceKind.Expression
            ? source.Expression?.Text
            : null;
        var dependencies = source.Kind == VisualValueSourceKind.Expression
            ? source.Expression?.Dependencies
            : source.TagReference is null
                ? null
                : new[]
                {
                    new VisualExpressionDependencyEngineeringDto(
                        "source",
                        source.Kind == VisualValueSourceKind.ClientMemory
                            ? VisualExpressionDependencyKind.ClientMemory
                            : VisualExpressionDependencyKind.Tag,
                        VisualExpressionValueType.Boolean,
                        source.TagReference,
                        source.Target)
                };

        if (source.Kind != VisualValueSourceKind.Expression && source.TagReference is null)
            throw new InvalidOperationException("Boolean Dynamo state sources require a stable TAG or Client Memory identity.");

        var inner = string.IsNullOrWhiteSpace(expressionText) ? "source" : $"({expressionText})";
        var normalizedExpression = invert ? $"number(not {inner})" : $"number({inner})";
        return new VisualValueSourceEngineeringDto(
            VisualValueSourceKind.Expression,
            VisualExpressionValueType.Number,
            Expression: new VisualExpressionEngineeringDto(
                normalizedExpression,
                VisualExpressionValueType.Number,
                dependencies));
    }

    private static VisualNavigationActionEngineeringDto ProjectAction(
        VisualNavigationActionEngineeringDto action,
        IReadOnlyDictionary<string, DynamoParameterValueEngineeringDto> parameters)
    {
        if (!string.IsNullOrWhiteSpace(action.CommandParameterKey))
        {
            if (!parameters.TryGetValue(action.CommandParameterKey, out var commandValue) ||
                commandValue.Kind != DynamoParameterKind.Command ||
                !commandValue.CommandId.HasValue || commandValue.CommandId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Dynamo action '{action.EventKey}' requires mapped Command parameter '{action.CommandParameterKey}'.");
            action = action with { CommandId = commandValue.CommandId, CommandParameterKey = null };
        }

        if ((action.Kind is VisualNavigationActionKind.SetTagValue or VisualNavigationActionKind.ToggleTagBoolean) &&
            TryParameterToken(action.TargetKey, out var targetParameterKey))
        {
            if (!parameters.TryGetValue(targetParameterKey, out var targetValue) ||
                targetValue.Kind != DynamoParameterKind.TagReference ||
                targetValue.TagReference is null || targetValue.TagReference.TagId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Dynamo action '{action.EventKey}' requires a mapped TagReference parameter '{targetParameterKey}'.");
            action = action with { TargetKey = targetValue.TagReference.TagId.ToString("D") };
        }

        if (action.Parameters is { Count: > 0 })
        {
            var projected = new Dictionary<string, JsonElement>(action.Parameters, StringComparer.Ordinal);
            foreach (var (key, value) in action.Parameters)
            {
                if (value.ValueKind != JsonValueKind.String || !TryParameterToken(value.GetString(), out var parameterKey))
                    continue;
                if (!parameters.TryGetValue(parameterKey, out var parameter) || parameter.Value is not { } parameterValue ||
                    parameter.Kind is DynamoParameterKind.TagReference or DynamoParameterKind.Command or DynamoParameterKind.EquipmentPath)
                    throw new InvalidOperationException(
                        $"Dynamo action '{action.EventKey}' requires a scalar parameter '{parameterKey}'.");
                projected[key] = parameterValue;
            }
            action = action with { Parameters = projected };
        }

        return action;
    }

    private static bool TryParameterToken(string? value, out string parameterKey)
    {
        parameterKey = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length < 3 || value[0] != '{' || value[^1] != '}')
            return false;
        parameterKey = value[1..^1].Trim();
        return parameterKey.Length > 0;
    }

    private static bool RequiresInstanceProjection(
        IReadOnlyCollection<VisualElementEngineeringDto> elements) =>
        elements.Any(element =>
            element.Metadata?.ContainsKey("dynamoStateColorParameter") == true ||
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true ||
            element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true ||
            element.BooleanConditions?.Any(condition => condition.Source?.Target?.StartsWith("{dynamoParameter:", StringComparison.Ordinal) == true) == true ||
            element.Actions?.Any(action => !string.IsNullOrWhiteSpace(action?.CommandParameterKey)) == true ||
            element.Actions?.Any(action => TryParameterToken(action?.TargetKey, out _) ||
                action?.Parameters?.Values.Any(value => value.ValueKind == JsonValueKind.String && TryParameterToken(value.GetString(), out _)) == true) == true ||
            (element.Children is { Count: > 0 } && RequiresInstanceProjection(element.Children)));

    public static string RuntimeElementIdentity(Guid instanceId, Guid definitionElementId) =>
        $"{instanceId:D}/{definitionElementId:D}";
}
