using Scada.Core.Alarms;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

public static class EngineeringValidator
{
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

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
