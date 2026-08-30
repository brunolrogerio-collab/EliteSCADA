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

        if (string.IsNullOrWhiteSpace(instance.DynamoKey) ||
            !instance.DynamoKey.Equals(definition.Key, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Visual element does not reference the supplied Dynamo definition.", nameof(instance));
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
            definition.Elements ?? Array.Empty<VisualElementEngineeringDto>());
    }

    public static string RuntimeElementIdentity(Guid instanceId, Guid definitionElementId) =>
        $"{instanceId:D}/{definitionElementId:D}";
}
