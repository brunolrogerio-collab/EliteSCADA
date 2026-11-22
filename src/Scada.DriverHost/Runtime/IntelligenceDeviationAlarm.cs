using System.Security.Cryptography;
using System.Text;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Runtime;

internal static class IntelligenceDeviationAlarm
{
    private static readonly TimeSpan MaximumSampleAge = TimeSpan.FromSeconds(60);

    public static Guid GetId(Guid contextId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"scadivo:intelligence:deviation:{contextId:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static (TagValue Value, bool Active)? Evaluate(
        CurrentTagCache cache,
        AssetProcessIntelligenceDeviationAlarmEngineeringDto alarm,
        IReadOnlyCollection<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates,
        DateTimeOffset now)
    {
        var target = ReadCurrent(cache, alarm.TagId, now);
        if (target is null || NumericValue(target.Value) is not { } number)
            return null;

        var matches = true;
        foreach (var predicate in predicates)
        {
            var reading = ReadCurrent(cache, predicate.TagId, now);
            if (reading is null || Matches(predicate, reading.Value) is not { } match)
                return null;
            matches &= match;
        }

        return (target, matches && (number < alarm.Minimum || number > alarm.Maximum));
    }

    private static TagValue? ReadCurrent(CurrentTagCache cache, Guid tagId, DateTimeOffset now) =>
        cache.TryGet(tagId, out var value) && value is { Quality: TagQuality.Good, Value: not null } &&
        value.Timestamp <= now && now - value.Timestamp <= MaximumSampleAge ? value : null;

    private static bool? Matches(AssetProcessIntelligenceOperatingContextPredicateEngineeringDto predicate, object? value) =>
        predicate.Kind switch
        {
            AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval when NumericValue(value) is { } number =>
                number >= (predicate.Minimum ?? double.NegativeInfinity) &&
                number <= (predicate.Maximum ?? double.PositiveInfinity),
            AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals when value is bool boolean =>
                boolean == predicate.BooleanValue,
            AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals or
                AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals when value is string text =>
                string.Equals(text, predicate.ExactValue, StringComparison.Ordinal),
            _ => null
        };

    private static double? NumericValue(object? value) => value switch
    {
        short number => number,
        int number => number,
        long number => number,
        float number when float.IsFinite(number) => number,
        double number when double.IsFinite(number) => number,
        _ => null
    };
}
