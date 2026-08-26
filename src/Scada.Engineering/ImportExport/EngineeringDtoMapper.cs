using System.Globalization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport;

internal static class EngineeringDtoMapper
{
    public static TagEngineeringDto ToDto(TagDefinition tag)
    {
        var address = Meta(tag.Metadata, "address");
        var minimum = Meta(tag.Metadata, "scale.minimum");
        var maximum = Meta(tag.Metadata, "scale.maximum");
        var historianEnabled = Meta(tag.Metadata, "historian.enabled");
        var historianStrategy = Meta(tag.Metadata, "historian.strategy");
        var deadband = Meta(tag.Metadata, "historian.deadband");
        var period = Meta(tag.Metadata, "historian.periodMs");
        var maximumPeriod = Meta(tag.Metadata, "historian.maxPeriodMs");
        var accessPolicy = tag.AccessPolicy is null
            ? null
            : new TagAccessPolicyDto(
                tag.AccessPolicy.ReadRoles?.ToArray(),
                tag.AccessPolicy.WriteRoles?.ToArray(),
                tag.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagEngineeringDto(
            tag.Id,
            tag.Name,
            tag.Path,
            tag.DataType,
            tag.Source,
            address,
            tag.EngineeringUnit,
            tag.Description,
            tag.ReadOnly,
            DoubleOrNull(minimum),
            DoubleOrNull(maximum),
            new HistorianSettingsDto(
                Bool(historianEnabled),
                historianStrategy ?? "none",
                DoubleOrNull(deadband),
                IntOrNull(period),
                IntOrNull(maximumPeriod)),
            tag.Metadata?.ToDictionary(x => x.Key, x => x.Value),
            accessPolicy);
    }

    public static AlarmEngineeringDto ToDto(AlarmDefinition alarm, string? tagPath) =>
        new(
            alarm.Id,
            alarm.Name,
            alarm.TagId,
            tagPath,
            alarm.Type,
            alarm.Priority,
            alarm.Setpoint,
            alarm.DigitalActiveValue,
            alarm.AlarmClass,
            alarm.Area,
            alarm.Message,
            alarm.ActivationDelay.HasValue ? (int)alarm.ActivationDelay.Value.TotalMilliseconds : null,
            alarm.RequiresAcknowledgement,
            alarm.ShelvingAllowed,
            alarm.Enabled,
            alarm.Metadata?.ToDictionary(x => x.Key, x => x.Value));

    private static string? Meta(IReadOnlyDictionary<string, string>? metadata, string key) =>
        metadata is not null && metadata.TryGetValue(key, out var value) ? value : null;

    private static double? DoubleOrNull(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static int? IntOrNull(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool Bool(string? value, bool fallback = false) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;
}
