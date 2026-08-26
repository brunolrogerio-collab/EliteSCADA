using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Historian.Policies;

public enum HistorianBucketWidth
{
    OneMinute,
    FiveMinutes,
    FifteenMinutes,
    OneHour
}

public static class HistorianBucketWidthExtensions
{
    public static TimeSpan ToTimeSpan(this HistorianBucketWidth width) => width switch
    {
        HistorianBucketWidth.OneMinute => TimeSpan.FromMinutes(1),
        HistorianBucketWidth.FiveMinutes => TimeSpan.FromMinutes(5),
        HistorianBucketWidth.FifteenMinutes => TimeSpan.FromMinutes(15),
        HistorianBucketWidth.OneHour => TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(width), width, "Unsupported historian bucket width.")
    };
}

public sealed record HistorianRetentionRule(
    bool Enabled = false,
    TimeSpan? Duration = null)
{
    public void Validate(string name)
    {
        if (Enabled && (Duration is null || Duration <= TimeSpan.Zero))
            throw new ArgumentException($"{name} retention requires a positive duration when enabled.", name);
        if (!Enabled && Duration is not null)
            throw new ArgumentException($"{name} retention duration must be null when retention is disabled.", name);
    }
}

public sealed record HistorianDownsamplingRule(
    HistorianBucketWidth Bucket,
    bool Enabled = true,
    TimeSpan? RefreshInterval = null,
    TimeSpan? RefreshLookback = null,
    HistorianRetentionRule? Retention = null)
{
    public HistorianRetentionRule EffectiveRetention => Retention ?? new HistorianRetentionRule();

    public void Validate()
    {
        _ = Bucket.ToTimeSpan();
        EffectiveRetention.Validate($"aggregate {Bucket}");

        if (!Enabled)
        {
            if (RefreshInterval is not null || RefreshLookback is not null)
                throw new ArgumentException($"Disabled downsampling tier '{Bucket}' cannot define refresh timing.");
            return;
        }

        if (RefreshInterval is null || RefreshInterval <= TimeSpan.Zero)
            throw new ArgumentException($"Downsampling tier '{Bucket}' requires a positive refresh interval.");
        if (RefreshLookback is null || RefreshLookback <= Bucket.ToTimeSpan())
            throw new ArgumentException($"Downsampling tier '{Bucket}' refresh lookback must be greater than one bucket.");
        if (RefreshLookback < RefreshInterval)
            throw new ArgumentException($"Downsampling tier '{Bucket}' refresh lookback cannot be shorter than its refresh interval.");
        if (EffectiveRetention.Enabled && EffectiveRetention.Duration < Bucket.ToTimeSpan())
            throw new ArgumentException($"Aggregate retention for '{Bucket}' cannot be shorter than one bucket.");
    }
}

public sealed record HistorianStoragePolicy(
    string Key,
    HistorianRetentionRule RawRetention,
    IReadOnlyCollection<HistorianDownsamplingRule>? Downsampling = null)
{
    public IReadOnlyCollection<HistorianDownsamplingRule> EffectiveDownsampling =>
        Downsampling ?? Array.Empty<HistorianDownsamplingRule>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("Historian storage policy key is required.", nameof(Key));
        if (Key.Any(char.IsWhiteSpace))
            throw new ArgumentException("Historian storage policy key cannot contain whitespace.", nameof(Key));

        ArgumentNullException.ThrowIfNull(RawRetention);
        RawRetention.Validate("raw");

        foreach (var rule in EffectiveDownsampling)
        {
            ArgumentNullException.ThrowIfNull(rule);
            rule.Validate();
        }

        var duplicate = EffectiveDownsampling
            .GroupBy(x => x.Bucket)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Historian storage policy contains duplicate downsampling tier '{duplicate.Key}'.", nameof(Downsampling));
    }
}

public sealed record HistorianPolicyApplyOptions(
    bool AllowPotentialDataExpiration = false);

public static class HistorianPolicySafety
{
    public static bool RequiresExplicitDataExpirationApproval(
        HistorianStoragePolicy? current,
        HistorianStoragePolicy next)
    {
        ArgumentNullException.ThrowIfNull(next);
        next.Validate();

        if (BecomesMoreDestructive(current?.RawRetention, next.RawRetention))
            return true;

        var currentByBucket = current?.EffectiveDownsampling.ToDictionary(x => x.Bucket)
            ?? new Dictionary<HistorianBucketWidth, HistorianDownsamplingRule>();

        foreach (var nextTier in next.EffectiveDownsampling)
        {
            currentByBucket.TryGetValue(nextTier.Bucket, out var currentTier);
            if (BecomesMoreDestructive(currentTier?.EffectiveRetention, nextTier.EffectiveRetention))
                return true;
        }

        return false;
    }

    private static bool BecomesMoreDestructive(HistorianRetentionRule? current, HistorianRetentionRule next)
    {
        current ??= new HistorianRetentionRule();
        if (!next.Enabled) return false;
        if (!current.Enabled) return true;
        return next.Duration < current.Duration;
    }
}

public static class HistorianPolicyJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(HistorianStoragePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        return JsonSerializer.Serialize(policy, Options);
    }

    public static HistorianStoragePolicy Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Historian storage policy JSON is required.", nameof(json));

        var policy = JsonSerializer.Deserialize<HistorianStoragePolicy>(json, Options)
            ?? throw new JsonException("Historian storage policy JSON produced a null policy.");
        policy.Validate();
        return policy;
    }
}
