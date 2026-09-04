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
        if (element.Type.Equals(BuiltinVisualObjectSchemas.SliderType, StringComparison.Ordinal))
            issues.AddRange(ValidateSlider(element, entityKind, entityKey));
        if (element.Type.Equals(BuiltinVisualObjectSchemas.AlarmBrowserType, StringComparison.Ordinal) ||
            element.Type.Equals(BuiltinVisualObjectSchemas.EventBrowserType, StringComparison.Ordinal))
            issues.AddRange(ValidateBrowserConfiguration(element, entityKind, entityKey));
        return issues;
    }

    private static IEnumerable<ImportIssue> ValidateSlider(
        VisualElementEngineeringDto element,
        ImportEntityKind entityKind,
        string entityKey)
    {
        var minimum = ReadNumber(element, VisualPropertyKeys.Minimum, 0);
        var maximum = ReadNumber(element, VisualPropertyKeys.Maximum, 100);
        var step = ReadNumber(element, VisualPropertyKeys.Step, 1);

        if (minimum >= maximum)
        {
            yield return Error(
                "VISUAL_SLIDER_RANGE_INVALID",
                $"Slider '{element.Key}' requires minimum to be less than maximum.",
                entityKind,
                entityKey);
        }

        if (step <= 0)
        {
            yield return Error(
                "VISUAL_SLIDER_STEP_INVALID",
                $"Slider '{element.Key}' requires a positive step.",
                entityKind,
                entityKey);
        }

        if (!ReadBoolean(element, VisualPropertyKeys.InteractionEnabled, false))
            yield break;

        var valueBindings = (element.Bindings ?? Array.Empty<EngineeringBindingDto>())
            .Where(binding => binding is not null &&
                binding.Key.Equals(VisualPropertyKeys.Value, StringComparison.Ordinal))
            .ToArray();
        var valueBinding = valueBindings.Length == 1 ? valueBindings[0] : null;
        if (valueBinding is null ||
            valueBinding.Kind != EngineeringBindingKind.Tag ||
            valueBinding.TagReference is null ||
            valueBinding.TagReference.TagId == Guid.Empty ||
            valueBinding.TagReference.Selector is not null ||
            !IsWriteDirection(valueBinding.Direction))
        {
            yield return Error(
                "VISUAL_SLIDER_WRITABLE_TAG_REQUIRED",
                $"Interactive Slider '{element.Key}' requires one numeric TAG value binding with stable identity and readWrite/write direction.",
                entityKind,
                entityKey);
        }
    }

    private static IEnumerable<ImportIssue> ValidateBrowserConfiguration(
        VisualElementEngineeringDto element,
        ImportEntityKind entityKind,
        string entityKey)
    {
        if (element.Properties is null ||
            !element.Properties.TryGetValue(BuiltinVisualObjectSchemas.BrowserConfigProperty, out var configuration))
            yield break;

        if (configuration.ValueKind != JsonValueKind.Object)
        {
            yield return Error(
                "VISUAL_BROWSER_CONFIG_INVALID",
                $"Browser '{element.Key}' requires browserConfig to be a JSON object.",
                entityKind,
                entityKey);
            yield break;
        }

        if (configuration.TryGetProperty("version", out var version) &&
            (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1))
        {
            yield return Error(
                "VISUAL_BROWSER_CONFIG_VERSION_UNSUPPORTED",
                $"Browser '{element.Key}' has an unsupported browserConfig version.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("columns", out var columns) &&
            (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() == 0))
        {
            yield return Error(
                "VISUAL_BROWSER_COLUMNS_INVALID",
                $"Browser '{element.Key}' must display at least one configured column.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("pageSize", out var pageSize) &&
            (pageSize.ValueKind != JsonValueKind.Number || !pageSize.TryGetInt32(out var page) || page is < 10 or > 200))
        {
            yield return Error(
                "VISUAL_BROWSER_PAGE_SIZE_INVALID",
                $"Browser '{element.Key}' pageSize must be between 10 and 200.",
                entityKind,
                entityKey);
        }
    }

    private static double ReadNumber(
        VisualElementEngineeringDto element,
        string key,
        double fallback) =>
        element.Properties is not null &&
        element.Properties.TryGetValue(key, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : fallback;

    private static bool ReadBoolean(
        VisualElementEngineeringDto element,
        string key,
        bool fallback) =>
        element.Properties is not null && element.Properties.TryGetValue(key, out var value)
            ? value.ValueKind == JsonValueKind.True ||
              (value.ValueKind == JsonValueKind.False ? false : fallback)
            : fallback;

    private static bool IsWriteDirection(string? direction)
    {
        var normalized = direction?.Trim().ToLowerInvariant();
        return normalized is "write" or "readwrite" or "read-write" or "bidirectional" or "twoway" or "two-way";
    }

    private static Dictionary<string, JsonElement>? ScalarProperties(VisualElementEngineeringDto element)
    {
        if (element.Properties is null) return null;

        var structuralProperty = element.Type switch
        {
            BuiltinVisualObjectSchemas.PolygonType => "points",
            BuiltinVisualObjectSchemas.TrendType => BuiltinVisualObjectSchemas.TrendPensProperty,
            BuiltinVisualObjectSchemas.AlarmBrowserType => BuiltinVisualObjectSchemas.BrowserConfigProperty,
            BuiltinVisualObjectSchemas.EventBrowserType => BuiltinVisualObjectSchemas.BrowserConfigProperty,
            _ => null
        };

        if (structuralProperty is null || !element.Properties.ContainsKey(structuralProperty))
            return element.Properties;

        return element.Properties
            .Where(property => !property.Key.Equals(structuralProperty, StringComparison.Ordinal))
            .ToDictionary(property => property.Key, property => property.Value, StringComparer.Ordinal);
    }

    private static ImportIssue Error(
        string code,
        string message,
        ImportEntityKind kind,
        string key) => new(code, message, kind, key, true);
}
