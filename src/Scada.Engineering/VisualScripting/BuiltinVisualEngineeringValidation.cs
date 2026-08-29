using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualScripting;

public static class BuiltinVisualEngineeringValidation
{
    public static IReadOnlyCollection<ImportIssue> Validate(
        VisualElementEngineeringDto element,
        ImportEntityKind entityKind,
        string entityKey,
        int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(element);

        var issues = new List<ImportIssue>();
        var schema = BuiltinVisualObjectSchemas.All.SingleOrDefault(
            candidate => candidate.ObjectTypeKey.Equals(element.Type, StringComparison.Ordinal));

        if (schema is null)
        {
            if (element.Type?.StartsWith("core.", StringComparison.Ordinal) == true)
            {
                issues.Add(Error(
                    "VISUAL_BUILTIN_TYPE_UNKNOWN",
                    $"Visual element '{element.Key}' references unknown built-in type '{element.Type}'.",
                    entityKind,
                    entityKey));
            }
            return issues;
        }

        try
        {
            _ = VisualEngineeringPropertyCodec.Decode(
                schema,
                ScalarProperties(element),
                allowLegacyStringValues: schemaVersion < VisualEngineeringPropertyCodec.TypedSchemaVersion);
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidDataException or ArgumentException)
        {
            issues.Add(Error(
                "VISUAL_PROPERTY_INVALID",
                $"Visual element '{element.Key}' has invalid properties for '{element.Type}': {error.Message}",
                entityKind,
                entityKey));
        }

        foreach (var binding in element.Bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            // Generic Engineering validation owns malformed/null binding diagnostics.
            // Built-in schema validation must never turn the same untrusted input
            // into an exception while Preview is collecting issues.
            if (binding is null || string.IsNullOrWhiteSpace(binding.Key))
                continue;

            if (!schema.Declares(binding.Key))
            {
                issues.Add(Error(
                    "VISUAL_BINDING_PROPERTY_UNKNOWN",
                    $"Binding '{binding.Key}' on visual element '{element.Key}' targets a property not declared by '{element.Type}'.",
                    entityKind,
                    entityKey));
                continue;
            }

            if (!schema.GetRequired(binding.Key).SupportsBinding)
            {
                issues.Add(Error(
                    "VISUAL_BINDING_PROPERTY_NOT_SUPPORTED",
                    $"Visual property '{binding.Key}' on '{element.Key}' does not support bindings.",
                    entityKind,
                    entityKey));
            }
        }

        issues.AddRange(VisualDynamicEngineeringValidation.Validate(element, schema, entityKind, entityKey));
        return issues;
    }

    private static Dictionary<string, JsonElement>? ScalarProperties(VisualElementEngineeringDto element)
    {
        if (element.Properties is null) return null;
        if (!element.Type.Equals(BuiltinVisualObjectSchemas.PolygonType, StringComparison.Ordinal) ||
            !element.Properties.ContainsKey("points"))
            return element.Properties;

        return element.Properties
            .Where(property => !property.Key.Equals("points", StringComparison.Ordinal))
            .ToDictionary(property => property.Key, property => property.Value, StringComparer.Ordinal);
    }

    private static ImportIssue Error(
        string code,
        string message,
        ImportEntityKind kind,
        string key) => new(code, message, kind, key, true);
}
