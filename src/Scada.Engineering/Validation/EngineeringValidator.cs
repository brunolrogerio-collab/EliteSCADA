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

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
