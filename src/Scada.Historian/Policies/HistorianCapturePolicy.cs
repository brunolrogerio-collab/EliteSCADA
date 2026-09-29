using System.Globalization;
using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Historian.Policies;

public enum HistorianCaptureMode
{
    LegacyAll,
    Periodic,
    OnChange,
    OnChangeDeadband,
    OnChangeDeadbandMaxInterval
}

public enum HistorianCaptureDisposition
{
    Accepted,
    Skipped,
    Coalesced
}

public sealed record HistorianCapturePolicySettings(
    bool Enabled,
    HistorianCaptureMode Mode,
    TimeSpan? Period = null,
    double? Deadband = null,
    TimeSpan? MaximumInterval = null)
{
    internal string Signature =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Enabled}|{Mode}|{Period?.Ticks ?? 0}|{Deadband?.ToString("R", CultureInfo.InvariantCulture) ?? "-"}|{MaximumInterval?.Ticks ?? 0}");
}

public sealed record HistorianCaptureDecision(
    HistorianCaptureDisposition Disposition,
    HistorianCapturePolicySettings Policy,
    string Reason)
{
    public bool Accepted => Disposition == HistorianCaptureDisposition.Accepted;
}

/// <summary>
/// Resolves the effective Runtime capture metadata and evaluates source observations.
/// TAGs without explicit historian.enabled retain legacy capture-all behavior.
/// Invalid legacy inline settings also fail open to legacy capture-all so introducing
/// the R2 policy engine cannot silently discard history. Canonical reusable profiles
/// are validated before Runtime activation and therefore reach this engine as valid
/// metadata.
/// </summary>
public static class HistorianCapturePolicy
{
    public const string EnabledMetadataKey = "historian.enabled";
    public const string StrategyMetadataKey = "historian.strategy";
    public const string DeadbandMetadataKey = "historian.deadband";
    public const string PeriodMetadataKey = "historian.periodMs";
    public const string MaximumPeriodMetadataKey = "historian.maxPeriodMs";

    public static bool ShouldCapture(TagDefinition tag) => Resolve(tag).Enabled;

    public static HistorianCapturePolicySettings Resolve(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Metadata is null || !tag.Metadata.TryGetValue(EnabledMetadataKey, out var enabledText))
            return Legacy(enabled: true);

        if (!bool.TryParse(enabledText, out var enabled) || !enabled)
            return Legacy(enabled: false);

        if (!tag.Metadata.TryGetValue(StrategyMetadataKey, out var strategyText) ||
            string.IsNullOrWhiteSpace(strategyText))
            return Legacy(enabled: true);

        var strategy = Normalize(strategyText);
        switch (strategy)
        {
            case "periodic":
                return TryMilliseconds(tag.Metadata, PeriodMetadataKey, out var period)
                    ? new(true, HistorianCaptureMode.Periodic, Period: period)
                    : Legacy(enabled: true);

            case "onchange":
            case "change":
                return new(true, HistorianCaptureMode.OnChange);

            case "onchangedeadband":
            case "deadband":
                return IsNumeric(tag.DataType) &&
                       TryDeadband(tag.Metadata, out var deadband)
                    ? new(true, HistorianCaptureMode.OnChangeDeadband, Deadband: deadband)
                    : Legacy(enabled: true);

            case "onchangedeadbandmaxinterval":
                return IsNumeric(tag.DataType) &&
                       TryDeadband(tag.Metadata, out var boundedDeadband) &&
                       TryMilliseconds(tag.Metadata, MaximumPeriodMetadataKey, out var maximumInterval)
                    ? new(
                        true,
                        HistorianCaptureMode.OnChangeDeadbandMaxInterval,
                        Deadband: boundedDeadband,
                        MaximumInterval: maximumInterval)
                    : Legacy(enabled: true);

            default:
                // Legacy "none" and unrecognized free-text strategies historically
                // captured every enabled source observation.
                return Legacy(enabled: true);
        }
    }

    private static HistorianCapturePolicySettings Legacy(bool enabled) =>
        new(enabled, HistorianCaptureMode.LegacyAll);

    private static bool TryDeadband(IReadOnlyDictionary<string, string> metadata, out double deadband)
    {
        deadband = default;
        return metadata.TryGetValue(DeadbandMetadataKey, out var text) &&
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out deadband) &&
               double.IsFinite(deadband) &&
               deadband >= 0d;
    }

    private static bool TryMilliseconds(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        out TimeSpan value)
    {
        value = default;
        return metadata.TryGetValue(key, out var text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) &&
               milliseconds > 0 &&
               TryTimeSpan(milliseconds, out value);
    }

    private static bool TryTimeSpan(int milliseconds, out TimeSpan value)
    {
        try
        {
            value = TimeSpan.FromMilliseconds(milliseconds);
            return value > TimeSpan.Zero;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static string Normalize(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static bool IsNumeric(TagDataType dataType) =>
        dataType is TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double;
}

/// <summary>
/// Stateful admission engine shared by in-memory and TimescaleDB Historians.
/// State advances only when an observation is accepted, so deadband and bounded
/// maximum-interval decisions are always measured from the last persisted sample.
/// No timer exists here: source silence can never manufacture a fresh sample.
/// </summary>
public sealed class HistorianCaptureAdmission
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CaptureState> _state = new();

    public HistorianCaptureDecision Evaluate(TagValueChanged observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var policy = HistorianCapturePolicy.Resolve(observation.Tag);
        lock (_sync)
        {
            if (!policy.Enabled)
            {
                _state.Remove(observation.Current.TagId);
                return new(HistorianCaptureDisposition.Skipped, policy, "disabled");
            }

            if (!_state.TryGetValue(observation.Current.TagId, out var state) ||
                !string.Equals(state.PolicySignature, policy.Signature, StringComparison.Ordinal))
            {
                _state[observation.Current.TagId] = new(policy.Signature, observation.Current);
                return new(HistorianCaptureDisposition.Accepted, policy, "first-observation");
            }

            var current = observation.Current;
            var previousAccepted = state.LastAccepted;

            if (current.Quality != previousAccepted.Quality)
            {
                state.LastAccepted = current;
                return new(HistorianCaptureDisposition.Accepted, policy, "quality-transition");
            }

            switch (policy.Mode)
            {
                case HistorianCaptureMode.LegacyAll:
                    state.LastAccepted = current;
                    return new(HistorianCaptureDisposition.Accepted, policy, "legacy-all");

                case HistorianCaptureMode.Periodic:
                    if (HasElapsed(previousAccepted.Timestamp, current.Timestamp, policy.Period!.Value))
                    {
                        state.LastAccepted = current;
                        return new(HistorianCaptureDisposition.Accepted, policy, "period-elapsed");
                    }
                    return new(HistorianCaptureDisposition.Coalesced, policy, "period-window");

                case HistorianCaptureMode.OnChange:
                    if (!ValuesEqual(previousAccepted.Value, current.Value))
                    {
                        state.LastAccepted = current;
                        return new(HistorianCaptureDisposition.Accepted, policy, "value-transition");
                    }
                    return new(HistorianCaptureDisposition.Skipped, policy, "steady-state");

                case HistorianCaptureMode.OnChangeDeadband:
                    return EvaluateDeadband(state, policy, current, bounded: false);

                case HistorianCaptureMode.OnChangeDeadbandMaxInterval:
                    return EvaluateDeadband(state, policy, current, bounded: true);

                default:
                    throw new InvalidOperationException($"Unsupported Historian capture mode '{policy.Mode}'.");
            }
        }
    }

    public void Reset()
    {
        lock (_sync)
            _state.Clear();
    }

    private static HistorianCaptureDecision EvaluateDeadband(
        CaptureState state,
        HistorianCapturePolicySettings policy,
        TagValue current,
        bool bounded)
    {
        var previousAccepted = state.LastAccepted;

        if (bounded &&
            HasElapsed(previousAccepted.Timestamp, current.Timestamp, policy.MaximumInterval!.Value))
        {
            state.LastAccepted = current;
            return new(HistorianCaptureDisposition.Accepted, policy, "maximum-interval");
        }

        if (ValuesEqual(previousAccepted.Value, current.Value))
            return new(HistorianCaptureDisposition.Skipped, policy, "steady-state");

        if (!TryNumeric(previousAccepted.Value, out var previousNumeric) ||
            !TryNumeric(current.Value, out var currentNumeric))
        {
            // Runtime profile activation rejects incompatible declared TAG types.
            // A malformed runtime value is not safe to suppress as if deadband had
            // been evaluated successfully, so preserve the actual observation.
            state.LastAccepted = current;
            return new(HistorianCaptureDisposition.Accepted, policy, "non-numeric-observation");
        }

        var delta = Math.Abs(currentNumeric - previousNumeric);
        if (double.IsNaN(delta) || delta >= policy.Deadband!.Value)
        {
            state.LastAccepted = current;
            return new(HistorianCaptureDisposition.Accepted, policy, "deadband-crossing");
        }

        return new(HistorianCaptureDisposition.Skipped, policy, "below-deadband");
    }

    private static bool HasElapsed(DateTimeOffset previous, DateTimeOffset current, TimeSpan interval) =>
        current >= previous && current - previous >= interval;

    private static bool ValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Equals(right)) return true;

        return TryNumeric(left, out var leftNumeric) &&
               TryNumeric(right, out var rightNumeric) &&
               leftNumeric.Equals(rightNumeric);
    }

    private static bool TryNumeric(object? value, out double number)
    {
        switch (value)
        {
            case sbyte v: number = v; return true;
            case byte v: number = v; return true;
            case short v: number = v; return true;
            case ushort v: number = v; return true;
            case int v: number = v; return true;
            case uint v: number = v; return true;
            case long v: number = v; return true;
            case ulong v: number = v; return true;
            case float v when float.IsFinite(v): number = v; return true;
            case double v when double.IsFinite(v): number = v; return true;
            case decimal v:
                number = (double)v;
                return double.IsFinite(number);
            default:
                number = default;
                return false;
        }
    }

    private sealed class CaptureState(string policySignature, TagValue lastAccepted)
    {
        public string PolicySignature { get; } = policySignature;
        public TagValue LastAccepted { get; set; } = lastAccepted;
    }
}
