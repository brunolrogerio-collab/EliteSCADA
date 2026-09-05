using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualScripting;

public static class BuiltinVisualEngineeringValidation
{
    private static readonly string[] AlarmBrowserColumns =
    [
        "timestamp", "state", "priority", "name", "area", "tag.path", "message", "acknowledgedBy"
    ];

    private static readonly string[] EventBrowserColumns =
    [
        "timestamp", "type", "category", "source", "area", "equipment.path", "tag.path",
        "operator", "operation", "command.key", "message"
    ];

    private static readonly string[] AlarmBrowserSortFields = ["timestamp", "state", "priority", "tag.path"];
    private static readonly string[] EventBrowserSortFields = ["timestamp", "type", "category", "source", "area", "tag.path"];

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
            yield return BrowserError(
                "VISUAL_BROWSER_CONFIG_INVALID",
                element,
                "browserConfig must be a JSON object.",
                entityKind,
                entityKey);
            yield break;
        }

        var isAlarm = element.Type.Equals(BuiltinVisualObjectSchemas.AlarmBrowserType, StringComparison.Ordinal);
        var allowedColumns = isAlarm ? AlarmBrowserColumns : EventBrowserColumns;
        var allowedSortFields = isAlarm ? AlarmBrowserSortFields : EventBrowserSortFields;

        if (configuration.TryGetProperty("version", out var version) &&
            (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionNumber) || versionNumber != 1))
        {
            yield return BrowserError(
                "VISUAL_BROWSER_CONFIG_VERSION_UNSUPPORTED",
                element,
                "browserConfig version must be 1.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("columns", out var columns))
        {
            if (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() == 0)
            {
                yield return BrowserError(
                    "VISUAL_BROWSER_COLUMNS_INVALID",
                    element,
                    "at least one visible column is required.",
                    entityKind,
                    entityKey);
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var column in columns.EnumerateArray())
                {
                    var value = column.ValueKind == JsonValueKind.String ? column.GetString() : null;
                    if (value is null || !allowedColumns.Contains(value, StringComparer.Ordinal))
                    {
                        yield return BrowserError(
                            "VISUAL_BROWSER_COLUMNS_INVALID",
                            element,
                            $"column '{value ?? column.ToString()}' is not supported by '{element.Type}'.",
                            entityKind,
                            entityKey);
                        break;
                    }
                    if (!seen.Add(value))
                    {
                        yield return BrowserError(
                            "VISUAL_BROWSER_COLUMNS_INVALID",
                            element,
                            $"column '{value}' is duplicated.",
                            entityKind,
                            entityKey);
                        break;
                    }
                }
            }
        }

        if (configuration.TryGetProperty("pageSize", out var pageSize) &&
            !IsIntegerInRange(pageSize, 10, 200))
        {
            yield return BrowserError(
                "VISUAL_BROWSER_PAGE_SIZE_INVALID",
                element,
                "pageSize must be an integer between 10 and 200.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("lookbackSeconds", out var lookback) &&
            !IsIntegerInRange(lookback, 60, 2_678_400))
        {
            yield return BrowserError(
                "VISUAL_BROWSER_LOOKBACK_INVALID",
                element,
                "lookbackSeconds must be an integer between 60 and 2678400.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("sortField", out var sortField) &&
            !IsAllowedString(sortField, allowedSortFields))
        {
            yield return BrowserError(
                "VISUAL_BROWSER_SORT_INVALID",
                element,
                "sortField is not supported by this Browser type.",
                entityKind,
                entityKey);
        }

        if (configuration.TryGetProperty("sortDirection", out var sortDirection) &&
            !IsAllowedString(sortDirection, ["ascending", "descending"]))
        {
            yield return BrowserError(
                "VISUAL_BROWSER_SORT_INVALID",
                element,
                "sortDirection must be ascending or descending.",
                entityKind,
                entityKey);
        }

        if (isAlarm)
        {
            if (configuration.TryGetProperty("mode", out var mode) &&
                !IsAllowedString(mode, ["current", "history"]))
                yield return BrowserError("VISUAL_BROWSER_MODE_INVALID", element, "mode must be current or history.", entityKind, entityKey);

            if (configuration.TryGetProperty("lifecycle", out var lifecycle) &&
                !IsAllowedString(lifecycle, ["all", "active", "returned"]))
                yield return BrowserError("VISUAL_BROWSER_FILTER_INVALID", element, "lifecycle is invalid.", entityKind, entityKey);

            if (configuration.TryGetProperty("acknowledgement", out var acknowledgement) &&
                !IsAllowedString(acknowledgement, ["all", "acknowledged", "unacknowledged"]))
                yield return BrowserError("VISUAL_BROWSER_FILTER_INVALID", element, "acknowledgement is invalid.", entityKind, entityKey);

            if (configuration.TryGetProperty("minimumPriority", out var priority) &&
                priority.ValueKind != JsonValueKind.Null &&
                !IsIntegerInRange(priority, 1, 4))
                yield return BrowserError("VISUAL_BROWSER_FILTER_INVALID", element, "minimumPriority must be null or an integer from 1 to 4.", entityKind, entityKey);

            if (configuration.TryGetProperty("acknowledgeEnabled", out var acknowledgeEnabled) &&
                acknowledgeEnabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                yield return BrowserError("VISUAL_BROWSER_CONFIG_INVALID", element, "acknowledgeEnabled must be boolean.", entityKind, entityKey);

            foreach (var issue in ValidateBrowserText(configuration, element, entityKind, entityKey,
                         ("area", 240), ("tagPath", 500), ("text", 500)))
                yield return issue;
        }
        else
        {
            foreach (var issue in ValidateBrowserText(configuration, element, entityKind, entityKey,
                         ("type", 120), ("category", 120), ("source", 240), ("area", 240),
                         ("equipmentPath", 500), ("tagPath", 500), ("operator", 240),
                         ("operation", 240), ("commandKey", 240), ("text", 500)))
                yield return issue;
        }
    }

    private static IEnumerable<ImportIssue> ValidateBrowserText(
        JsonElement configuration,
        VisualElementEngineeringDto element,
        ImportEntityKind entityKind,
        string entityKey,
        params (string Key, int MaximumLength)[] fields)
    {
        foreach (var field in fields)
        {
            if (!configuration.TryGetProperty(field.Key, out var value)) continue;
            if (value.ValueKind != JsonValueKind.String)
            {
                yield return BrowserError(
                    "VISUAL_BROWSER_FILTER_INVALID",
                    element,
                    $"{field.Key} must be text.",
                    entityKind,
                    entityKey);
                continue;
            }

            var text = value.GetString() ?? string.Empty;
            if (text.Trim().Length > field.MaximumLength || text.Any(char.IsControl))
            {
                yield return BrowserError(
                    "VISUAL_BROWSER_FILTER_INVALID",
                    element,
                    $"{field.Key} exceeds its supported text contract or contains control characters.",
                    entityKind,
                    entityKey);
            }
        }
    }

    private static bool IsIntegerInRange(JsonElement value, int minimum, int maximum) =>
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number) &&
        number >= minimum &&
        number <= maximum;

    private static bool IsAllowedString(JsonElement value, IReadOnlyCollection<string> allowed) =>
        value.ValueKind == JsonValueKind.String &&
        value.GetString() is { } text &&
        allowed.Contains(text, StringComparer.Ordinal);

    private static ImportIssue BrowserError(
        string code,
        VisualElementEngineeringDto element,
        string detail,
        ImportEntityKind kind,
        string key) =>
        Error(code, $"Browser '{element.Key}' {detail}", kind, key);

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
