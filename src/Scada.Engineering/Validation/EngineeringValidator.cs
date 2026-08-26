using Scada.Core.Alarms;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

public static class EngineeringValidator
{
    private static readonly string[] SensitiveKeyFragments =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
        "privatekey", "private_key", "credential", "clientsecret", "client_secret"
    };

    private static readonly string[] SensitiveValueFragments =
    {
        "password=", "passwd=", "pwd=", "token=", "apikey=", "api_key=", "clientsecret=", "client_secret="
    };

    private static readonly string[] AllowedSecretReferencePrefixes =
    {
        "secret://", "env://", "vault://", "keyvault://"
    };

    public static IReadOnlyCollection<ImportIssue> ValidateTag(TagEngineeringDto tag)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(tag.Path) ? tag.Name : tag.Path;
        if (string.IsNullOrWhiteSpace(tag.Name)) issues.Add(Error("TAG_NAME_REQUIRED", "Tag name is required.", ImportEntityKind.Tag, key));
        if (string.IsNullOrWhiteSpace(tag.Path)) issues.Add(Error("TAG_PATH_REQUIRED", "Tag path is required.", ImportEntityKind.Tag, key));
        if (tag.Path?.Any(char.IsWhiteSpace) == true) issues.Add(Error("TAG_PATH_WHITESPACE", "Tag path cannot contain whitespace.", ImportEntityKind.Tag, key));
        if (tag.ScaleMinimum.HasValue && tag.ScaleMaximum.HasValue && tag.ScaleMinimum >= tag.ScaleMaximum)
            issues.Add(Error("TAG_SCALE_INVALID", "ScaleMinimum must be less than ScaleMaximum.", ImportEntityKind.Tag, key));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateAlarm(AlarmEngineeringDto alarm)
    {
        var issues = new List<ImportIssue>();
        var key = alarm.Name;
        if (string.IsNullOrWhiteSpace(alarm.Name)) issues.Add(Error("ALARM_NAME_REQUIRED", "Alarm name is required.", ImportEntityKind.Alarm, key));
        if (alarm.TagId is null && string.IsNullOrWhiteSpace(alarm.TagPath))
            issues.Add(Error("ALARM_TAG_REQUIRED", "Alarm must reference a tag by TagId or TagPath.", ImportEntityKind.Alarm, key));
        if ((alarm.Type is AlarmType.High or AlarmType.HighHigh or AlarmType.Low or AlarmType.LowLow) && alarm.Setpoint is null)
            issues.Add(Error("ALARM_SETPOINT_REQUIRED", "Analog alarm requires a setpoint.", ImportEntityKind.Alarm, key));
        if (alarm.ActivationDelayMilliseconds < 0)
            issues.Add(Error("ALARM_DELAY_INVALID", "Activation delay cannot be negative.", ImportEntityKind.Alarm, key));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateDataSource(DataSourceEngineeringDto dataSource)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(dataSource.Key) ? dataSource.Name : dataSource.Key;

        if (string.IsNullOrWhiteSpace(dataSource.Key))
            issues.Add(Error("DATASOURCE_KEY_REQUIRED", "Data source key is required.", ImportEntityKind.DataSource, key));
        if (dataSource.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("DATASOURCE_KEY_WHITESPACE", "Data source key cannot contain whitespace.", ImportEntityKind.DataSource, key));
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("DATASOURCE_NAME_REQUIRED", "Data source name is required.", ImportEntityKind.DataSource, key));
        if (string.IsNullOrWhiteSpace(dataSource.Driver))
            issues.Add(Error("DATASOURCE_DRIVER_REQUIRED", "Data source driver is required.", ImportEntityKind.DataSource, key));

        foreach (var setting in dataSource.Settings ?? [])
        {
            var normalizedKey = setting.Key.Replace("-", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
            if (SensitiveKeyFragments.Any(fragment => normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ||
                SensitiveValueFragments.Any(fragment => setting.Value.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(
                    "DATASOURCE_PLAINTEXT_SECRET",
                    $"Setting '{setting.Key}' appears to contain a secret. Store only a reference in secretReferences.",
                    ImportEntityKind.DataSource,
                    key));
            }
        }

        foreach (var secret in dataSource.SecretReferences ?? [])
        {
            if (string.IsNullOrWhiteSpace(secret.Key) || string.IsNullOrWhiteSpace(secret.Value))
            {
                issues.Add(Error("DATASOURCE_SECRET_REFERENCE_INVALID", "Secret reference name and value are required.", ImportEntityKind.DataSource, key));
                continue;
            }

            if (!AllowedSecretReferencePrefixes.Any(prefix => secret.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                issues.Add(Error("DATASOURCE_SECRET_REFERENCE_INVALID", $"Secret reference '{secret.Key}' must use secret://, env://, vault:// or keyvault://.", ImportEntityKind.DataSource, key));
        }

        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateTemplate(EquipmentTemplateEngineeringDto template)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(template.Key) ? template.Name : template.Key;
        if (string.IsNullOrWhiteSpace(template.Key))
            issues.Add(Error("TEMPLATE_KEY_REQUIRED", "Template key is required.", ImportEntityKind.Template, key));
        if (template.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("TEMPLATE_KEY_WHITESPACE", "Template key cannot contain whitespace.", ImportEntityKind.Template, key));
        if (string.IsNullOrWhiteSpace(template.Name))
            issues.Add(Error("TEMPLATE_NAME_REQUIRED", "Template name is required.", ImportEntityKind.Template, key));
        issues.AddRange(ValidateBindings(template.Bindings, ImportEntityKind.Template, key, allowTagPlaceholders: true));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateEquipment(EquipmentEngineeringDto equipment)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(equipment.Path) ? equipment.Name : equipment.Path;
        if (string.IsNullOrWhiteSpace(equipment.Path))
            issues.Add(Error("EQUIPMENT_PATH_REQUIRED", "Equipment path is required.", ImportEntityKind.Equipment, key));
        if (equipment.Path?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("EQUIPMENT_PATH_WHITESPACE", "Equipment path cannot contain whitespace.", ImportEntityKind.Equipment, key));
        if (string.IsNullOrWhiteSpace(equipment.Name))
            issues.Add(Error("EQUIPMENT_NAME_REQUIRED", "Equipment name is required.", ImportEntityKind.Equipment, key));
        if (equipment.TemplateKey?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("EQUIPMENT_TEMPLATE_KEY_WHITESPACE", "Equipment template key cannot contain whitespace.", ImportEntityKind.Equipment, key));
        issues.AddRange(ValidateBindings(equipment.Bindings, ImportEntityKind.Equipment, key, allowTagPlaceholders: false));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateDynamo(DynamoEngineeringDto dynamo)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(dynamo.Key) ? dynamo.Name : dynamo.Key;
        if (string.IsNullOrWhiteSpace(dynamo.Key))
            issues.Add(Error("DYNAMO_KEY_REQUIRED", "Dynamo key is required.", ImportEntityKind.Dynamo, key));
        if (dynamo.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("DYNAMO_KEY_WHITESPACE", "Dynamo key cannot contain whitespace.", ImportEntityKind.Dynamo, key));
        if (string.IsNullOrWhiteSpace(dynamo.Name))
            issues.Add(Error("DYNAMO_NAME_REQUIRED", "Dynamo name is required.", ImportEntityKind.Dynamo, key));
        if (dynamo.TemplateKey?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("DYNAMO_TEMPLATE_KEY_WHITESPACE", "Dynamo template key cannot contain whitespace.", ImportEntityKind.Dynamo, key));
        issues.AddRange(ValidateBindings(dynamo.Bindings, ImportEntityKind.Dynamo, key, allowTagPlaceholders: true));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateScreen(ScreenEngineeringDto screen)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(screen.Key) ? screen.Name : screen.Key;
        if (string.IsNullOrWhiteSpace(screen.Key))
            issues.Add(Error("SCREEN_KEY_REQUIRED", "Screen key is required.", ImportEntityKind.Screen, key));
        if (screen.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("SCREEN_KEY_WHITESPACE", "Screen key cannot contain whitespace.", ImportEntityKind.Screen, key));
        if (string.IsNullOrWhiteSpace(screen.Name))
            issues.Add(Error("SCREEN_NAME_REQUIRED", "Screen name is required.", ImportEntityKind.Screen, key));
        if (!string.IsNullOrWhiteSpace(screen.Route) && !screen.Route.StartsWith('/', StringComparison.Ordinal))
            issues.Add(Error("SCREEN_ROUTE_INVALID", "Screen route must start with '/'.", ImportEntityKind.Screen, key));
        issues.AddRange(ValidateVisualElements(screen.Elements, ImportEntityKind.Screen, key, allowPlaceholders: false));
        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidatePopup(PopupEngineeringDto popup)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(popup.Key) ? popup.Name : popup.Key;
        if (string.IsNullOrWhiteSpace(popup.Key))
            issues.Add(Error("POPUP_KEY_REQUIRED", "Popup key is required.", ImportEntityKind.Popup, key));
        if (popup.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("POPUP_KEY_WHITESPACE", "Popup key cannot contain whitespace.", ImportEntityKind.Popup, key));
        if (string.IsNullOrWhiteSpace(popup.Name))
            issues.Add(Error("POPUP_NAME_REQUIRED", "Popup name is required.", ImportEntityKind.Popup, key));
        if (popup.TemplateKey?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("POPUP_TEMPLATE_KEY_WHITESPACE", "Popup template key cannot contain whitespace.", ImportEntityKind.Popup, key));
        issues.AddRange(ValidateVisualElements(popup.Elements, ImportEntityKind.Popup, key, allowPlaceholders: true));
        return issues;
    }

    private static IEnumerable<ImportIssue> ValidateVisualElements(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        ImportEntityKind entityKind,
        string entityKey,
        bool allowPlaceholders)
    {
        if (elements is null) yield break;

        var duplicates = elements
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            if (string.IsNullOrWhiteSpace(element.Key))
                yield return Error("VISUAL_ELEMENT_KEY_REQUIRED", "Visual element key is required.", entityKind, entityKey);
            if (string.IsNullOrWhiteSpace(element.Type))
                yield return Error("VISUAL_ELEMENT_TYPE_REQUIRED", $"Visual element '{element.Key}' requires a type.", entityKind, entityKey);
            if (!string.IsNullOrWhiteSpace(element.Key) && duplicates.Contains(element.Key))
                yield return Error("VISUAL_ELEMENT_DUPLICATE", $"Visual element key '{element.Key}' appears more than once at the same level.", entityKind, entityKey);
            if (!string.IsNullOrWhiteSpace(element.DynamoKey) && element.DynamoKey.Any(char.IsWhiteSpace))
                yield return Error("VISUAL_DYNAMO_KEY_WHITESPACE", $"Dynamo key on element '{element.Key}' cannot contain whitespace.", entityKind, entityKey);
            if (!string.IsNullOrWhiteSpace(element.EquipmentPath) && !allowPlaceholders && ContainsPlaceholder(element.EquipmentPath))
                yield return Error("VISUAL_EQUIPMENT_PLACEHOLDER_NOT_ALLOWED", $"Screen element '{element.Key}' must reference a concrete equipment path.", entityKind, entityKey);

            foreach (var issue in ValidateBindings(element.Bindings, entityKind, entityKey, allowTagPlaceholders: allowPlaceholders))
                yield return issue;
            foreach (var issue in ValidateVisualElements(element.Children, entityKind, entityKey, allowPlaceholders))
                yield return issue;
        }
    }

    private static IEnumerable<ImportIssue> ValidateBindings(
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        ImportEntityKind entityKind,
        string entityKey,
        bool allowTagPlaceholders)
    {
        if (bindings is null) yield break;

        var duplicates = bindings
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Key))
                yield return Error("BINDING_KEY_REQUIRED", "Binding key is required.", entityKind, entityKey);
            if (string.IsNullOrWhiteSpace(binding.Target))
                yield return Error("BINDING_TARGET_REQUIRED", $"Binding '{binding.Key}' requires a target.", entityKind, entityKey);
            if (!string.IsNullOrWhiteSpace(binding.Key) && duplicates.Contains(binding.Key))
                yield return Error("BINDING_DUPLICATE", $"Binding key '{binding.Key}' appears more than once.", entityKind, entityKey);
            if (binding.Kind == EngineeringBindingKind.Tag && !allowTagPlaceholders && ContainsPlaceholder(binding.Target))
                yield return Error("BINDING_TAG_PLACEHOLDER_NOT_ALLOWED", $"Binding '{binding.Key}' must reference a concrete TAG path.", entityKind, entityKey);
        }
    }

    private static bool ContainsPlaceholder(string value) => value.Contains('{', StringComparison.Ordinal) || value.Contains('}', StringComparison.Ordinal);

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
