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
            Bindings = element.Bindings?.Select(binding => binding with
            {
                Target = normalizedPath is null ? binding.Target : binding.Target.Replace("{equipmentPath}", normalizedPath, StringComparison.Ordinal)
            }).ToArray(),
            Actions = element.Actions?.Select(action =>
            {
                if (string.IsNullOrWhiteSpace(action.CommandParameterKey))
                    return action;
                if (!parameters.TryGetValue(action.CommandParameterKey, out var commandValue) ||
                    commandValue.Kind != DynamoParameterKind.Command ||
                    !commandValue.CommandId.HasValue ||
                    commandValue.CommandId == Guid.Empty)
                    throw new InvalidOperationException(
                        $"Dynamo action '{action.EventKey}' requires mapped Command parameter '{action.CommandParameterKey}'.");
                return action with
                {
                    CommandId = commandValue.CommandId,
                    CommandParameterKey = null
                };
            }).ToArray(),
            PropertyMaps = element.PropertyMaps?.Select(map =>
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
                return map with
                {
                    Source = map.Source with
                    {
                        Target = normalizedPath is null ? map.Source.Target : map.Source.Target?.Replace("{equipmentPath}", normalizedPath, StringComparison.Ordinal),
                        TagReference = stateParameter?.Kind == DynamoParameterKind.TagReference
                            ? stateParameter.TagReference
                            : map.Source.TagReference
                    },
                    Rules = rules
                };
            }).ToArray(),
            Children = SubstituteInstanceContext(
                element.Children ?? Array.Empty<VisualElementEngineeringDto>(),
                normalizedPath,
                parameters)
        }).ToArray();
    }

    private static bool RequiresInstanceProjection(
        IReadOnlyCollection<VisualElementEngineeringDto> elements) =>
        elements.Any(element =>
            element.Metadata?.ContainsKey("dynamoStateColorParameter") == true ||
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true ||
            element.Actions?.Any(action => !string.IsNullOrWhiteSpace(action?.CommandParameterKey)) == true ||
            (element.Children is { Count: > 0 } && RequiresInstanceProjection(element.Children)));

    public static string RuntimeElementIdentity(Guid instanceId, Guid definitionElementId) =>
        $"{instanceId:D}/{definitionElementId:D}";
}
