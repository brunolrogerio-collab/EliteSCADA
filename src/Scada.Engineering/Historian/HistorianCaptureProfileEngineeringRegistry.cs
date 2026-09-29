using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Historian;

public sealed record HistorianCaptureProfileValidationProblem(string Code, string Message);

public static class HistorianCaptureProfileEngineeringValidation
{
    public static IReadOnlyCollection<HistorianCaptureProfileValidationProblem> Validate(
        HistorianCaptureProfileEngineeringDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var problems = new List<HistorianCaptureProfileValidationProblem>();

        if (profile.Id == Guid.Empty)
            problems.Add(new("HISTORIAN_CAPTURE_PROFILE_ID_INVALID", "Historian capture profile Id cannot be empty."));
        if (string.IsNullOrWhiteSpace(profile.Key))
            problems.Add(new("HISTORIAN_CAPTURE_PROFILE_KEY_REQUIRED", "Historian capture profile Key is required."));
        if (string.IsNullOrWhiteSpace(profile.Name))
            problems.Add(new("HISTORIAN_CAPTURE_PROFILE_NAME_REQUIRED", "Historian capture profile Name is required."));
        if (profile.Version != R2SharedEngineeringContractVersions.HistorianCaptureProfile)
            problems.Add(new(
                "HISTORIAN_CAPTURE_PROFILE_VERSION_UNSUPPORTED",
                $"Historian capture profile version {profile.Version} is unsupported; expected {R2SharedEngineeringContractVersions.HistorianCaptureProfile}."));

        if (profile.PeriodMilliseconds.HasValue && profile.PeriodMilliseconds.Value <= 0)
            problems.Add(new("HISTORIAN_CAPTURE_PERIOD_INVALID", "Historian capture PeriodMilliseconds must be greater than zero when supplied."));
        if (profile.MaximumIntervalMilliseconds.HasValue && profile.MaximumIntervalMilliseconds.Value <= 0)
            problems.Add(new("HISTORIAN_CAPTURE_MAX_INTERVAL_INVALID", "Historian capture MaximumIntervalMilliseconds must be greater than zero when supplied."));
        if (profile.Deadband.HasValue &&
            (!double.IsFinite(profile.Deadband.Value) || profile.Deadband.Value < 0d))
        {
            problems.Add(new(
                "HISTORIAN_CAPTURE_DEADBAND_INVALID",
                "Historian capture Deadband must be a finite non-negative number when supplied."));
        }

        switch (profile.Strategy)
        {
            case HistorianCaptureStrategy.Periodic when !profile.PeriodMilliseconds.HasValue:
                problems.Add(new(
                    "HISTORIAN_CAPTURE_PERIOD_REQUIRED",
                    "PERIODIC Historian capture requires PeriodMilliseconds."));
                break;
            case HistorianCaptureStrategy.OnChangeDeadband when !profile.Deadband.HasValue:
            case HistorianCaptureStrategy.OnChangeDeadbandMaxInterval when !profile.Deadband.HasValue:
                problems.Add(new(
                    "HISTORIAN_CAPTURE_DEADBAND_REQUIRED",
                    $"{profile.Strategy} Historian capture requires Deadband."));
                break;
        }

        if (profile.Strategy == HistorianCaptureStrategy.OnChangeDeadbandMaxInterval &&
            !profile.MaximumIntervalMilliseconds.HasValue)
        {
            problems.Add(new(
                "HISTORIAN_CAPTURE_MAX_INTERVAL_REQUIRED",
                "ON_CHANGE_DEADBAND_MAX_INTERVAL Historian capture requires MaximumIntervalMilliseconds."));
        }

        return problems;
    }

    public static IReadOnlyCollection<HistorianCaptureProfileValidationProblem> ValidateForTag(
        HistorianCaptureProfileEngineeringDto profile,
        TagDataType dataType)
    {
        var problems = Validate(profile).ToList();
        if (UsesDeadband(profile.Strategy) && !IsNumeric(dataType))
        {
            problems.Add(new(
                "HISTORIAN_CAPTURE_DEADBAND_TYPE_INCOMPATIBLE",
                $"Historian deadband capture is only valid for numeric TAGs; data type '{dataType}' is incompatible."));
        }

        return problems;
    }

    public static bool UsesDeadband(HistorianCaptureStrategy strategy) =>
        strategy is HistorianCaptureStrategy.OnChangeDeadband or
            HistorianCaptureStrategy.OnChangeDeadbandMaxInterval;

    public static bool IsNumeric(TagDataType dataType) =>
        dataType is TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double;
}

public interface IHistorianCaptureProfileEngineeringRegistry
{
    IReadOnlyCollection<HistorianCaptureProfileEngineeringDto> Snapshot();
    HistorianCaptureProfileEngineeringDto? Find(Guid id);
    HistorianCaptureProfileEngineeringDto? FindByKey(string key);
    void Upsert(HistorianCaptureProfileEngineeringDto profile);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryHistorianCaptureProfileEngineeringRegistry : IHistorianCaptureProfileEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, HistorianCaptureProfileEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryHistorianCaptureProfileEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<HistorianCaptureProfileEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public HistorianCaptureProfileEngineeringDto? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public HistorianCaptureProfileEngineeringDto? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key.Trim(), out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(HistorianCaptureProfileEngineeringDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var problems = HistorianCaptureProfileEngineeringValidation.Validate(profile);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(x => x.Message)), nameof(profile));

        var normalized = profile with
        {
            Id = profile.Id ?? Guid.NewGuid(),
            Key = profile.Key.Trim(),
            Name = profile.Name.Trim()
        };
        var id = normalized.Id!.Value;

        lock (_sync)
        {
            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
            {
                _byKey.Remove(previous.Key);
            }

            if (_byKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _byId.Remove(otherId);

            _byId[id] = normalized;
            _byKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        HistorianCaptureProfileEngineeringDto? removed;
        lock (_sync)
        {
            if (!_byId.Remove(id, out removed)) return false;
            _byKey.Remove(removed.Key);
        }

        _changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }

        _changed?.Invoke();
    }
}
