using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authorization;

namespace Scada.Engineering.DataQueries;

public sealed record DataQueryValidationProblem(string Code, string Message);

public static class DataQueryEngineeringValidation
{
    public const int MaximumSelectedFields = 64;
    public const int MaximumGroups = 16;
    public const int MaximumAggregates = 32;
    public const int MaximumParameters = 64;
    public const int MaximumBindings = 128;
    public const int MaximumAlarmFilterValues = 256;

    public static IReadOnlyCollection<DataQueryValidationProblem> Validate(DataQueryEngineeringDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var problems = new List<DataQueryValidationProblem>();

        if (definition.Id == Guid.Empty)
            problems.Add(new("DATA_QUERY_ID_INVALID", "Data Query Id cannot be empty."));
        if (string.IsNullOrWhiteSpace(definition.Key))
            problems.Add(new("DATA_QUERY_KEY_REQUIRED", "Data Query Key is required."));
        if (string.IsNullOrWhiteSpace(definition.Name))
            problems.Add(new("DATA_QUERY_NAME_REQUIRED", "Data Query Name is required."));
        if (string.IsNullOrWhiteSpace(definition.ProviderKey))
            problems.Add(new("DATA_QUERY_PROVIDER_REQUIRED", "Data Query ProviderKey is required."));
        if (definition.Version != R2SharedEngineeringContractVersions.DataQuery)
            problems.Add(new(
                "DATA_QUERY_VERSION_UNSUPPORTED",
                $"Data Query version {definition.Version} is unsupported; expected {R2SharedEngineeringContractVersions.DataQuery}."));

        HistoricalValidatedRequest? validated = null;
        if (definition.Query is null)
        {
            problems.Add(new("DATA_QUERY_HISTORICAL_QUERY_REQUIRED", "Data Query Historical Query descriptor is required."));
        }
        try
        {
            if (definition.Query is not null)
                validated = HistoricalQueryValidator.Validate(definition.Query);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            problems.Add(new("DATA_QUERY_HISTORICAL_QUERY_INVALID", FirstLine(ex.Message)));
        }

        var selected = definition.SelectedFields ?? Array.Empty<string>();
        if (selected.Count > MaximumSelectedFields)
            problems.Add(new("DATA_QUERY_SELECTED_FIELDS_LIMIT", $"Data Query cannot select more than {MaximumSelectedFields} fields."));
        if (selected.Any(string.IsNullOrWhiteSpace))
            problems.Add(new("DATA_QUERY_SELECTED_FIELD_INVALID", "Data Query selected fields cannot be blank."));
        if (selected.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x.Trim(), StringComparer.Ordinal)
            .Any(g => g.Count() > 1))
            problems.Add(new("DATA_QUERY_SELECTED_FIELD_DUPLICATE", "Data Query selected fields must be unique."));

        if (validated is not null)
        {
            foreach (var field in selected.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
            {
                if (!validated.Dataset.Fields.ContainsKey(field))
                    problems.Add(new("DATA_QUERY_SELECTED_FIELD_UNKNOWN", $"Field '{field}' is not available in dataset '{validated.Dataset.Id}'."));
            }
        }

        var groups = definition.Groups ?? Array.Empty<DataQueryGroupEngineeringDto>();
        if (groups.Count > MaximumGroups)
            problems.Add(new("DATA_QUERY_GROUP_LIMIT", $"Data Query cannot contain more than {MaximumGroups} groups."));
        foreach (var group in groups)
        {
            if (group is null || string.IsNullOrWhiteSpace(group.Field))
            {
                problems.Add(new("DATA_QUERY_GROUP_INVALID", "Data Query group field is required."));
                continue;
            }
            if (validated is not null && !validated.Dataset.Fields.ContainsKey(group.Field.Trim()))
                problems.Add(new("DATA_QUERY_GROUP_FIELD_UNKNOWN", $"Group field '{group.Field}' is not available in dataset '{validated.Dataset.Id}'."));
        }

        var aggregates = definition.Aggregates ?? Array.Empty<DataQueryAggregateEngineeringDto>();
        if (aggregates.Count > MaximumAggregates)
            problems.Add(new("DATA_QUERY_AGGREGATE_LIMIT", $"Data Query cannot contain more than {MaximumAggregates} aggregates."));
        if (aggregates.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
            problems.Add(new("DATA_QUERY_AGGREGATE_KEY_DUPLICATE", "Data Query aggregate keys must be unique."));
        foreach (var aggregate in aggregates)
        {
            if (aggregate is null || string.IsNullOrWhiteSpace(aggregate.Key) || string.IsNullOrWhiteSpace(aggregate.Field))
            {
                problems.Add(new("DATA_QUERY_AGGREGATE_INVALID", "Data Query aggregate requires Key and Field."));
                continue;
            }
            if (!Enum.IsDefined(aggregate.Function))
                problems.Add(new("DATA_QUERY_AGGREGATE_FUNCTION_INVALID", $"Aggregate '{aggregate.Key}' uses an unsupported function."));
            if (validated is not null && !validated.Dataset.Fields.ContainsKey(aggregate.Field.Trim()))
                problems.Add(new("DATA_QUERY_AGGREGATE_FIELD_UNKNOWN", $"Aggregate field '{aggregate.Field}' is not available in dataset '{validated.Dataset.Id}'."));
        }

        var parameters = definition.Parameters ?? Array.Empty<DataQueryParameterEngineeringDto>();
        if (parameters.Count > MaximumParameters)
            problems.Add(new("DATA_QUERY_PARAMETER_LIMIT", $"Data Query cannot contain more than {MaximumParameters} parameters."));
        var parameterByKey = new Dictionary<string, DataQueryParameterEngineeringDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (parameter is null || string.IsNullOrWhiteSpace(parameter.Key) || string.IsNullOrWhiteSpace(parameter.Name))
            {
                problems.Add(new("DATA_QUERY_PARAMETER_INVALID", "Data Query parameters require Key and Name."));
                continue;
            }

            var key = parameter.Key.Trim();
            if (!parameterByKey.TryAdd(key, parameter))
            {
                problems.Add(new("DATA_QUERY_PARAMETER_KEY_DUPLICATE", $"Data Query parameter key '{key}' is duplicated."));
                continue;
            }

            if (!Enum.IsDefined(parameter.Type))
                problems.Add(new("DATA_QUERY_PARAMETER_TYPE_INVALID", $"Data Query parameter '{key}' uses an unsupported type."));
            if (parameter.DefaultValue is not null && parameter.DefaultValue.Type != parameter.Type)
                problems.Add(new("DATA_QUERY_PARAMETER_DEFAULT_TYPE", $"Default value for parameter '{key}' does not match its declared type."));
            if (parameter.DefaultValue is not null && !TryNormalizeParameterValue(parameter.DefaultValue, out _))
                problems.Add(new("DATA_QUERY_PARAMETER_DEFAULT_INVALID", $"Default value for parameter '{key}' is invalid."));
            foreach (var allowed in parameter.AllowedValues ?? Array.Empty<DataQueryParameterValue>())
            {
                if (allowed.Type != parameter.Type || !TryNormalizeParameterValue(allowed, out _))
                    problems.Add(new("DATA_QUERY_PARAMETER_ALLOWED_INVALID", $"Allowed value for parameter '{key}' is invalid or has the wrong type."));
            }
        }

        var bindings = definition.ParameterBindings ?? Array.Empty<DataQueryParameterBindingEngineeringDto>();
        if (bindings.Count > MaximumBindings)
            problems.Add(new("DATA_QUERY_BINDING_LIMIT", $"Data Query cannot contain more than {MaximumBindings} parameter bindings."));
        foreach (var binding in bindings)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.ParameterKey))
            {
                problems.Add(new("DATA_QUERY_BINDING_INVALID", "Data Query parameter binding requires ParameterKey."));
                continue;
            }

            if (!parameterByKey.TryGetValue(binding.ParameterKey.Trim(), out var parameter))
            {
                problems.Add(new("DATA_QUERY_BINDING_PARAMETER_UNKNOWN", $"Data Query binding references unknown parameter '{binding.ParameterKey}'."));
                continue;
            }

            switch (binding.Target)
            {
                case DataQueryParameterTarget.AbsoluteFromUtc:
                case DataQueryParameterTarget.AbsoluteToUtc:
                case DataQueryParameterTarget.HistorianTargetUtc:
                    if (parameter.Type != DataQueryParameterType.DateTime)
                        problems.Add(new("DATA_QUERY_BINDING_TYPE", $"Binding target '{binding.Target}' requires a dateTime parameter."));
                    break;
                case DataQueryParameterTarget.RelativeDurationSeconds:
                    if (parameter.Type != DataQueryParameterType.DurationSeconds)
                        problems.Add(new("DATA_QUERY_BINDING_TYPE", "relativeDurationSeconds requires a durationSeconds parameter."));
                    break;
                case DataQueryParameterTarget.Search:
                    if (parameter.Type != DataQueryParameterType.String)
                        problems.Add(new("DATA_QUERY_BINDING_TYPE", "search requires a string parameter."));
                    break;
                case DataQueryParameterTarget.FilterValue:
                    if (!binding.FilterIndex.HasValue || !binding.ValueIndex.HasValue ||
                        binding.FilterIndex < 0 || binding.ValueIndex < 0 ||
                        definition.Query is null ||
                        definition.Query.Filters is null ||
                        binding.FilterIndex >= definition.Query.Filters.Count ||
                        binding.ValueIndex >= definition.Query.Filters[binding.FilterIndex.Value].Values.Count)
                        problems.Add(new("DATA_QUERY_FILTER_BINDING_INDEX", "filterValue binding requires valid FilterIndex and ValueIndex."));
                    break;
                default:
                    problems.Add(new("DATA_QUERY_BINDING_TARGET_INVALID", $"Binding target '{binding.Target}' is unsupported."));
                    break;
            }
        }

        ValidateRetrieval(definition, bindings, validated, problems);
        return problems;
    }

    public static IReadOnlyCollection<DataQueryValidationProblem> Validate(AlarmViewEngineeringDto view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var problems = new List<DataQueryValidationProblem>();
        if (view.Id == Guid.Empty)
            problems.Add(new("ALARM_VIEW_ID_INVALID", "Alarm View Id cannot be empty."));
        if (string.IsNullOrWhiteSpace(view.Key))
            problems.Add(new("ALARM_VIEW_KEY_REQUIRED", "Alarm View Key is required."));
        if (string.IsNullOrWhiteSpace(view.Name))
            problems.Add(new("ALARM_VIEW_NAME_REQUIRED", "Alarm View Name is required."));
        if (view.Version != R2SharedEngineeringContractVersions.AlarmView)
            problems.Add(new("ALARM_VIEW_VERSION_UNSUPPORTED", $"Alarm View version {view.Version} is unsupported; expected {R2SharedEngineeringContractVersions.AlarmView}."));
        if (view.Filter is null)
        {
            problems.Add(new("ALARM_VIEW_FILTER_REQUIRED", "Alarm View Filter is required."));
            return problems;
        }

        ValidateStrings(view.Filter.Areas, "areas", problems);
        ValidateStrings(view.Filter.AlarmClasses, "alarmClasses", problems);
        ValidateStrings(view.Filter.Categories, "categories", problems);
        ValidateStrings(view.Filter.Subconditions, "subconditions", problems);
        ValidateStrings(view.Filter.Sources, "sources", problems);
        ValidateGuids(view.Filter.AlarmIds, "alarmIds", problems);
        ValidateGuids(view.Filter.TagIds, "tagIds", problems);
        ValidateGuids(view.Filter.EquipmentIds, "equipmentIds", problems);

        if ((view.Filter.Priorities?.Count ?? 0) > MaximumAlarmFilterValues ||
            (view.Filter.Types?.Count ?? 0) > MaximumAlarmFilterValues)
            problems.Add(new("ALARM_VIEW_FILTER_LIMIT", $"Alarm View typed filter collections cannot exceed {MaximumAlarmFilterValues} values."));
        if (!Enum.IsDefined(view.Filter.Active) || !Enum.IsDefined(view.Filter.Acknowledged) || !Enum.IsDefined(view.Filter.Shelved))
            problems.Add(new("ALARM_VIEW_MATCH_STATE_INVALID", "Alarm View match-state value is invalid."));
        if (!string.IsNullOrEmpty(view.Filter.Search) && view.Filter.Search.Length > HistoricalQueryValidator.MaximumSearchLength)
            problems.Add(new("ALARM_VIEW_SEARCH_LIMIT", $"Alarm View search cannot exceed {HistoricalQueryValidator.MaximumSearchLength} characters."));

        return problems;
    }

    public static bool TryNormalizeParameterValue(DataQueryParameterValue value, out string normalized)
    {
        normalized = value.Value?.Trim() ?? string.Empty;
        switch (value.Type)
        {
            case DataQueryParameterType.String:
            case DataQueryParameterType.Enum:
                return value.Value is not null;
            case DataQueryParameterType.Boolean:
                if (!bool.TryParse(normalized, out var boolean)) return false;
                normalized = boolean ? "true" : "false";
                return true;
            case DataQueryParameterType.Number:
                if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    return false;
                normalized = number.ToString("R", CultureInfo.InvariantCulture);
                return true;
            case DataQueryParameterType.Int64:
                if (!long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return false;
                normalized = integer.ToString(CultureInfo.InvariantCulture);
                return true;
            case DataQueryParameterType.DurationSeconds:
                if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
                    return false;
                normalized = seconds.ToString(CultureInfo.InvariantCulture);
                return true;
            case DataQueryParameterType.Guid:
                if (!Guid.TryParse(normalized, out var guid) || guid == Guid.Empty) return false;
                normalized = guid.ToString("D");
                return true;
            case DataQueryParameterType.DateTime:
                if (!DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp) ||
                    timestamp.Offset != TimeSpan.Zero)
                    return false;
                normalized = timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static void ValidateRetrieval(
        DataQueryEngineeringDto definition,
        IReadOnlyCollection<DataQueryParameterBindingEngineeringDto> bindings,
        HistoricalValidatedRequest? validated,
        List<DataQueryValidationProblem> problems)
    {
        var retrieval = definition.HistorianRetrieval;
        if (retrieval is null) return;
        if (!Enum.IsDefined(retrieval.Mode))
        {
            problems.Add(new("DATA_QUERY_RETRIEVAL_MODE_INVALID", "Historian retrieval mode is invalid."));
            return;
        }

        if (retrieval.TargetUtc.HasValue && retrieval.TargetUtc.Value.Offset != TimeSpan.Zero)
            problems.Add(new("DATA_QUERY_TARGET_NOT_UTC", "Historian TargetUtc must use UTC offset +00:00."));
        if (retrieval.StepMilliseconds.HasValue && retrieval.StepMilliseconds <= 0)
            problems.Add(new("DATA_QUERY_STEP_INVALID", "Historian StepMilliseconds must be positive."));
        if (retrieval.BucketMilliseconds.HasValue && retrieval.BucketMilliseconds <= 0)
            problems.Add(new("DATA_QUERY_BUCKET_INVALID", "Historian BucketMilliseconds must be positive."));
        if (retrieval.MaximumGapMilliseconds.HasValue && retrieval.MaximumGapMilliseconds <= 0)
            problems.Add(new("DATA_QUERY_MAX_GAP_INVALID", "Historian MaximumGapMilliseconds must be positive."));

        if (retrieval.Mode != HistorianRetrievalMode.Raw &&
            validated is not null &&
            !string.Equals(validated.Dataset.Id, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal))
            problems.Add(new("DATA_QUERY_RETRIEVAL_DATASET", "Non-Raw Historian retrieval modes require dataset 'historian.samples'."));

        var targetBound = bindings.Any(x => x.Target == DataQueryParameterTarget.HistorianTargetUtc);
        if (retrieval.Mode is HistorianRetrievalMode.AtOrBefore or HistorianRetrievalMode.AtOrAfter or HistorianRetrievalMode.Exact or HistorianRetrievalMode.Interpolated)
        {
            if (!retrieval.TargetUtc.HasValue && !targetBound)
                problems.Add(new("DATA_QUERY_TARGET_REQUIRED", $"Historian retrieval mode '{retrieval.Mode}' requires TargetUtc or a historianTargetUtc parameter binding."));
        }

        if (retrieval.Mode is HistorianRetrievalMode.Interpolated or HistorianRetrievalMode.SampledFixedStep)
        {
            if (!retrieval.MaximumGapMilliseconds.HasValue || retrieval.MaximumGapMilliseconds <= 0)
                problems.Add(new("DATA_QUERY_MAX_GAP_REQUIRED", $"Historian retrieval mode '{retrieval.Mode}' requires positive MaximumGapMilliseconds."));
        }
        if (retrieval.Mode == HistorianRetrievalMode.SampledFixedStep &&
            (!retrieval.StepMilliseconds.HasValue || retrieval.StepMilliseconds <= 0))
            problems.Add(new("DATA_QUERY_STEP_REQUIRED", "SampledFixedStep requires positive StepMilliseconds."));
        if (retrieval.Mode == HistorianRetrievalMode.Aggregate)
        {
            if (!retrieval.BucketMilliseconds.HasValue || retrieval.BucketMilliseconds <= 0)
                problems.Add(new("DATA_QUERY_BUCKET_REQUIRED", "Aggregate retrieval requires positive BucketMilliseconds."));
            if (!retrieval.AggregateFunction.HasValue || !Enum.IsDefined(retrieval.AggregateFunction.Value))
                problems.Add(new("DATA_QUERY_AGGREGATE_FUNCTION_REQUIRED", "Aggregate retrieval requires AggregateFunction."));
        }
    }

    private static void ValidateStrings(
        IReadOnlyCollection<string>? values,
        string label,
        List<DataQueryValidationProblem> problems)
    {
        if (values is null) return;
        if (values.Count > MaximumAlarmFilterValues)
            problems.Add(new("ALARM_VIEW_FILTER_LIMIT", $"Alarm View {label} cannot exceed {MaximumAlarmFilterValues} values."));
        if (values.Any(string.IsNullOrWhiteSpace))
            problems.Add(new("ALARM_VIEW_FILTER_VALUE_INVALID", $"Alarm View {label} cannot contain blank values."));
    }

    private static void ValidateGuids(
        IReadOnlyCollection<Guid>? values,
        string label,
        List<DataQueryValidationProblem> problems)
    {
        if (values is null) return;
        if (values.Count > MaximumAlarmFilterValues)
            problems.Add(new("ALARM_VIEW_FILTER_LIMIT", $"Alarm View {label} cannot exceed {MaximumAlarmFilterValues} values."));
        if (values.Any(x => x == Guid.Empty))
            problems.Add(new("ALARM_VIEW_FILTER_VALUE_INVALID", $"Alarm View {label} cannot contain empty GUIDs."));
    }

    private static string FirstLine(string message)
    {
        var end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}

public interface IDataQueryEngineeringRegistry
{
    IReadOnlyCollection<DataQueryEngineeringDto> Snapshot();
    DataQueryEngineeringDto? Find(Guid id);
    DataQueryEngineeringDto? FindByKey(string key);
    void Upsert(DataQueryEngineeringDto definition);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryDataQueryEngineeringRegistry : IDataQueryEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, DataQueryEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryDataQueryEngineeringRegistry(Action? changed = null) => _changed = changed;

    public IReadOnlyCollection<DataQueryEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public DataQueryEngineeringDto? Find(Guid id)
    {
        lock (_sync) return _byId.GetValueOrDefault(id);
    }

    public DataQueryEngineeringDto? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key.Trim(), out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(DataQueryEngineeringDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var problems = DataQueryEngineeringValidation.Validate(definition);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(x => x.Message)), nameof(definition));

        lock (_sync)
        {
            var key = definition.Key.Trim();
            var existingByKey = _byKey.TryGetValue(key, out var byKeyId) ? _byId.GetValueOrDefault(byKeyId) : null;
            var id = definition.Id ?? existingByKey?.Id ?? Guid.NewGuid();
            if (id == Guid.Empty)
                throw new ArgumentException("Data Query Id cannot be empty.", nameof(definition));
            if (_byKey.TryGetValue(key, out var otherId) && otherId != id)
                throw new InvalidOperationException($"Data Query key '{key}' is already owned by stable Id '{otherId:D}'.");

            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            var normalized = definition with
            {
                Id = id,
                Key = key,
                Name = definition.Name.Trim(),
                ProviderKey = definition.ProviderKey.Trim()
            };
            _byId[id] = normalized;
            _byKey[key] = id;
        }
        _changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        DataQueryEngineeringDto? removed;
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

public interface IAlarmViewEngineeringRegistry
{
    IReadOnlyCollection<AlarmViewEngineeringDto> Snapshot();
    AlarmViewEngineeringDto? Find(Guid id);
    AlarmViewEngineeringDto? FindByKey(string key);
    void Upsert(AlarmViewEngineeringDto view);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryAlarmViewEngineeringRegistry : IAlarmViewEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, AlarmViewEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryAlarmViewEngineeringRegistry(Action? changed = null) => _changed = changed;

    public IReadOnlyCollection<AlarmViewEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public AlarmViewEngineeringDto? Find(Guid id)
    {
        lock (_sync) return _byId.GetValueOrDefault(id);
    }

    public AlarmViewEngineeringDto? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key.Trim(), out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(AlarmViewEngineeringDto view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var problems = DataQueryEngineeringValidation.Validate(view);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(x => x.Message)), nameof(view));

        lock (_sync)
        {
            var key = view.Key.Trim();
            var existingByKey = _byKey.TryGetValue(key, out var byKeyId) ? _byId.GetValueOrDefault(byKeyId) : null;
            var id = view.Id ?? existingByKey?.Id ?? Guid.NewGuid();
            if (id == Guid.Empty)
                throw new ArgumentException("Alarm View Id cannot be empty.", nameof(view));
            if (_byKey.TryGetValue(key, out var otherId) && otherId != id)
                throw new InvalidOperationException($"Alarm View key '{key}' is already owned by stable Id '{otherId:D}'.");

            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            var normalized = view with { Id = id, Key = key, Name = view.Name.Trim() };
            _byId[id] = normalized;
            _byKey[key] = id;
        }
        _changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        AlarmViewEngineeringDto? removed;
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

public sealed record AlarmViewFacts(
    Guid AlarmId,
    Guid TagId,
    AlarmPriority Priority,
    AlarmType Type,
    AlarmState State,
    string? Area = null,
    string? AlarmClass = null,
    string? Category = null,
    string? Subcondition = null,
    string? Source = null,
    Guid? EquipmentId = null,
    string? Message = null)
{
    public static AlarmViewFacts FromInstance(AlarmInstance alarm, string? alarmClass = null) =>
        new(
            alarm.DefinitionId,
            alarm.TagId,
            alarm.Priority,
            alarm.Type,
            alarm.State,
            alarm.Area,
            alarmClass,
            Message: alarm.Message);
}

public static class AlarmViewMatcher
{
    public static bool Matches(AlarmViewFilterEngineeringDto filter, AlarmViewFacts facts)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(facts);

        if (!Contains(filter.Areas, facts.Area)) return false;
        if (filter.Priorities is { Count: > 0 } && !filter.Priorities.Contains(facts.Priority)) return false;
        if (filter.Types is { Count: > 0 } && !filter.Types.Contains(facts.Type)) return false;
        if (!Contains(filter.AlarmClasses, facts.AlarmClass)) return false;
        if (!Contains(filter.Categories, facts.Category)) return false;
        if (!Contains(filter.Subconditions, facts.Subcondition)) return false;
        if (!Contains(filter.Sources, facts.Source)) return false;
        if (filter.AlarmIds is { Count: > 0 } && !filter.AlarmIds.Contains(facts.AlarmId)) return false;
        if (filter.TagIds is { Count: > 0 } && !filter.TagIds.Contains(facts.TagId)) return false;
        if (filter.EquipmentIds is { Count: > 0 } &&
            (!facts.EquipmentId.HasValue || !filter.EquipmentIds.Contains(facts.EquipmentId.Value))) return false;

        var active = facts.State is AlarmState.Active or AlarmState.Acknowledged;
        var acknowledged = facts.State == AlarmState.Acknowledged;
        var shelved = facts.State == AlarmState.Shelved;
        if (!MatchState(filter.Active, active) ||
            !MatchState(filter.Acknowledged, acknowledged) ||
            !MatchState(filter.Shelved, shelved))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var haystack = new[]
            {
                facts.Area, facts.AlarmClass, facts.Category, facts.Subcondition, facts.Source, facts.Message
            };
            if (!haystack.Any(x => x?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
                return false;
        }

        return true;
    }

    private static bool MatchState(AlarmViewMatchState expected, bool actual) => expected switch
    {
        AlarmViewMatchState.Any => true,
        AlarmViewMatchState.Yes => actual,
        AlarmViewMatchState.No => !actual,
        _ => false
    };

    private static bool Contains(IReadOnlyCollection<string>? allowed, string? candidate) =>
        allowed is not { Count: > 0 } ||
        (candidate is not null && allowed.Any(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// Adds Data Query and Alarm View authority to the existing Engineering package lifecycle
/// without moving their ownership into ImportExport.
/// </summary>
public sealed class DataQueryEngineeringExchangeDecorator : IEngineeringExchangeService
{
    private readonly IEngineeringExchangeService _inner;
    private readonly IDataQueryEngineeringRegistry _queries;
    private readonly IAlarmViewEngineeringRegistry _alarmViews;

    public DataQueryEngineeringExchangeDecorator(
        IEngineeringExchangeService inner,
        IDataQueryEngineeringRegistry queries,
        IAlarmViewEngineeringRegistry alarmViews)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _alarmViews = alarmViews ?? throw new ArgumentNullException(nameof(alarmViews));
    }

    public EngineeringPackage ExportPackage() =>
        _inner.ExportPackage() with
        {
            DataQueries = _queries.Snapshot(),
            AlarmViews = _alarmViews.Snapshot()
        };

    public string ExportJson(bool indented = true) =>
        JsonSerializer.Serialize(ExportPackage(), JsonOptions(indented));

    public string ExportTagsCsv() => _inner.ExportTagsCsv();
    public string ExportAlarmsCsv() => _inner.ExportAlarmsCsv();
    public string ExportDataSourcesCsv() => _inner.ExportDataSourcesCsv();
    public EngineeringPackage ParseJson(string json) => _inner.ParseJson(json);
    public EngineeringPackage ParseTagsCsv(string csv) => _inner.ParseTagsCsv(csv);
    public EngineeringPackage ParseAlarmsCsv(string csv) => _inner.ParseAlarmsCsv(csv);
    public EngineeringPackage ParseDataSourcesCsv(string csv) => _inner.ParseDataSourcesCsv(csv);

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode) =>
        Preview(package, mode, null);

    public ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var basePreview = _inner.Preview(package, mode, context);
        var items = basePreview.Items.Concat(PreviewOwned(package, mode)).ToArray();
        return BuildPreview(mode, items);
    }

    public ImportResult Apply(EngineeringPackage package, ImportMode mode) =>
        Apply(package, mode, null);

    public ImportResult Apply(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var preview = Preview(package, mode, context);
        if (!preview.CanApply)
            return new ImportResult(
                mode,
                0,
                0,
                preview.SkipCount,
                preview.Items.SelectMany(x => x.Issues).ToArray());

        var core = _inner.Apply(package, mode, context);
        if (core.Issues.Any(x => x.IsError))
            return core;

        var created = core.Created;
        var updated = core.Updated;
        var skipped = core.Skipped;

        foreach (var definition in package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
        {
            var existing = ResolveExisting(definition);
            if (existing is null)
            {
                if (mode == ImportMode.UpdateExisting) { skipped++; continue; }
                _queries.Upsert(definition);
                created++;
            }
            else
            {
                if (mode == ImportMode.CreateOnly) { skipped++; continue; }
                _queries.Upsert(definition with { Id = definition.Id ?? existing.Id });
                updated++;
            }
        }

        foreach (var view in package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
        {
            var existing = ResolveExisting(view);
            if (existing is null)
            {
                if (mode == ImportMode.UpdateExisting) { skipped++; continue; }
                _alarmViews.Upsert(view);
                created++;
            }
            else
            {
                if (mode == ImportMode.CreateOnly) { skipped++; continue; }
                _alarmViews.Upsert(view with { Id = view.Id ?? existing.Id });
                updated++;
            }
        }

        return new ImportResult(mode, created, updated, skipped, core.Issues);
    }

    private IEnumerable<ImportPreviewItem> PreviewOwned(EngineeringPackage package, ImportMode mode)
    {
        var seenQueryIds = new HashSet<Guid>();
        var seenQueryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
        {
            if (definition is null)
            {
                var issue = Issue("DATA_QUERY_NULL", "Data Query package entry cannot be null.", ImportEntityKind.DataQuery, "<null>");
                yield return new ImportPreviewItem(ImportEntityKind.DataQuery, "<null>", ImportOperation.Error, [issue]);
                continue;
            }

            var issues = DataQueryEngineeringValidation.Validate(definition)
                .Select(x => Issue(x.Code, x.Message, ImportEntityKind.DataQuery, definition.Key))
                .ToList();
            if (definition.Id.HasValue && !seenQueryIds.Add(definition.Id.Value))
                issues.Add(Issue("DATA_QUERY_ID_DUPLICATE", $"Data Query Id '{definition.Id!.Value:D}' is duplicated in the package.", ImportEntityKind.DataQuery, definition.Key));
            if (!string.IsNullOrWhiteSpace(definition.Key) && !seenQueryKeys.Add(definition.Key.Trim()))
                issues.Add(Issue("DATA_QUERY_KEY_DUPLICATE", $"Data Query key '{definition.Key}' is duplicated in the package.", ImportEntityKind.DataQuery, definition.Key));

            var existing = ResolveExisting(definition);
            var byKey = _queries.FindByKey(definition.Key);
            if (definition.Id.HasValue && byKey is not null && byKey.Id != definition.Id)
                issues.Add(Issue("DATA_QUERY_IDENTITY_CONFLICT", $"Data Query key '{definition.Key}' belongs to a different stable Id.", ImportEntityKind.DataQuery, definition.Key));

            yield return new ImportPreviewItem(
                ImportEntityKind.DataQuery,
                definition.Key,
                issues.Count > 0 ? ImportOperation.Error : Operation(mode, existing is not null),
                issues);
        }

        var seenViewIds = new HashSet<Guid>();
        var seenViewKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
        {
            if (view is null)
            {
                var issue = Issue("ALARM_VIEW_NULL", "Alarm View package entry cannot be null.", ImportEntityKind.AlarmView, "<null>");
                yield return new ImportPreviewItem(ImportEntityKind.AlarmView, "<null>", ImportOperation.Error, [issue]);
                continue;
            }

            var issues = DataQueryEngineeringValidation.Validate(view)
                .Select(x => Issue(x.Code, x.Message, ImportEntityKind.AlarmView, view.Key))
                .ToList();
            if (view.Id.HasValue && !seenViewIds.Add(view.Id.Value))
                issues.Add(Issue("ALARM_VIEW_ID_DUPLICATE", $"Alarm View Id '{view.Id!.Value:D}' is duplicated in the package.", ImportEntityKind.AlarmView, view.Key));
            if (!string.IsNullOrWhiteSpace(view.Key) && !seenViewKeys.Add(view.Key.Trim()))
                issues.Add(Issue("ALARM_VIEW_KEY_DUPLICATE", $"Alarm View key '{view.Key}' is duplicated in the package.", ImportEntityKind.AlarmView, view.Key));

            var existing = ResolveExisting(view);
            var byKey = _alarmViews.FindByKey(view.Key);
            if (view.Id.HasValue && byKey is not null && byKey.Id != view.Id)
                issues.Add(Issue("ALARM_VIEW_IDENTITY_CONFLICT", $"Alarm View key '{view.Key}' belongs to a different stable Id.", ImportEntityKind.AlarmView, view.Key));

            yield return new ImportPreviewItem(
                ImportEntityKind.AlarmView,
                view.Key,
                issues.Count > 0 ? ImportOperation.Error : Operation(mode, existing is not null),
                issues);
        }
    }

    private DataQueryEngineeringDto? ResolveExisting(DataQueryEngineeringDto definition) =>
        definition.Id.HasValue ? _queries.Find(definition.Id.Value) ?? _queries.FindByKey(definition.Key) : _queries.FindByKey(definition.Key);

    private AlarmViewEngineeringDto? ResolveExisting(AlarmViewEngineeringDto view) =>
        view.Id.HasValue ? _alarmViews.Find(view.Id.Value) ?? _alarmViews.FindByKey(view.Key) : _alarmViews.FindByKey(view.Key);

    private static ImportOperation Operation(ImportMode mode, bool exists) => (mode, exists) switch
    {
        (ImportMode.CreateOnly, false) => ImportOperation.Create,
        (ImportMode.CreateOnly, true) => ImportOperation.Skip,
        (ImportMode.UpdateExisting, false) => ImportOperation.Skip,
        (ImportMode.UpdateExisting, true) => ImportOperation.Update,
        (ImportMode.CreateAndUpdate, false) => ImportOperation.Create,
        (ImportMode.CreateAndUpdate, true) => ImportOperation.Update,
        _ => ImportOperation.Error
    };

    private static ImportPreview BuildPreview(ImportMode mode, IReadOnlyCollection<ImportPreviewItem> items) =>
        new(
            mode,
            items.Count(x => x.Operation == ImportOperation.Create),
            items.Count(x => x.Operation == ImportOperation.Update),
            items.Count(x => x.Operation == ImportOperation.Skip),
            items.Count(x => x.Operation == ImportOperation.Error),
            items);

    private static ImportIssue Issue(
        string code,
        string message,
        ImportEntityKind kind,
        string key) => new(code, message, kind, key, true);

    private static JsonSerializerOptions JsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented
        };
        options.Converters.Add(new SecurityCapabilityJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
