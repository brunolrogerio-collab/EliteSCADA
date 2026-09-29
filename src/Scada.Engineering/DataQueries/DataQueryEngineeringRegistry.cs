using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Engineering.DataQueries;

public sealed record DataQueryValidationProblem(string Code, string Message);

public static class DataQueryEngineeringValidation
{
    public const int MaximumParameterCount = 64;
    public const int MaximumSelectedFieldCount = 64;
    public const int MaximumAggregateCount = 32;
    public const int MaximumAlarmSearchLength = 200;

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
        try
        {
            validated = HistoricalQueryValidator.Validate(definition.Query);
        }
        catch (ArgumentException ex)
        {
            problems.Add(new("DATA_QUERY_HISTORICAL_QUERY_INVALID", FirstLine(ex.Message)));
        }

        if (definition.Query.Page?.Cursor is { Length: > 0 })
            problems.Add(new(
                "DATA_QUERY_CURSOR_NOT_PERSISTABLE",
                "Saved Data Query definitions cannot persist a runtime Historical Query cursor."));

        var selected = (definition.SelectedFields ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();
        if (selected.Length > MaximumSelectedFieldCount)
            problems.Add(new(
                "DATA_QUERY_SELECTED_FIELDS_LIMIT",
                $"Data Query cannot select more than {MaximumSelectedFieldCount} fields."));
        if (validated is not null)
        {
            foreach (var field in selected)
            {
                if (!validated.Dataset.Fields.ContainsKey(field))
                    problems.Add(new(
                        "DATA_QUERY_FIELD_UNKNOWN",
                        $"Selected field '{field}' is not defined by dataset '{validated.Dataset.Id}'."));
            }
        }

        var groups = definition.Groups ?? Array.Empty<DataQueryGroupEngineeringDto>();
        if (validated is not null)
        {
            foreach (var group in groups)
            {
                if (group is null || string.IsNullOrWhiteSpace(group.Field) ||
                    !validated.Dataset.Fields.ContainsKey(group.Field.Trim()))
                {
                    problems.Add(new(
                        "DATA_QUERY_GROUP_FIELD_UNKNOWN",
                        $"Data Query group field '{group?.Field}' is invalid for dataset '{validated.Dataset.Id}'."));
                }
            }
        }

        var aggregates = definition.Aggregates ?? Array.Empty<DataQueryAggregateEngineeringDto>();
        if (aggregates.Count > MaximumAggregateCount)
            problems.Add(new(
                "DATA_QUERY_AGGREGATE_LIMIT",
                $"Data Query cannot declare more than {MaximumAggregateCount} aggregates."));
        var aggregateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in aggregates)
        {
            if (aggregate is null || string.IsNullOrWhiteSpace(aggregate.Key))
            {
                problems.Add(new("DATA_QUERY_AGGREGATE_KEY_REQUIRED", "Data Query aggregate Key is required."));
                continue;
            }
            if (!aggregateKeys.Add(aggregate.Key.Trim()))
                problems.Add(new(
                    "DATA_QUERY_AGGREGATE_KEY_DUPLICATE",
                    $"Data Query aggregate key '{aggregate.Key}' is duplicated."));
            if (validated is not null &&
                (string.IsNullOrWhiteSpace(aggregate.Field) ||
                 !validated.Dataset.Fields.ContainsKey(aggregate.Field.Trim())))
            {
                problems.Add(new(
                    "DATA_QUERY_AGGREGATE_FIELD_UNKNOWN",
                    $"Aggregate field '{aggregate.Field}' is not defined by dataset '{validated.Dataset.Id}'."));
            }
        }

        var parameters = definition.Parameters ?? Array.Empty<DataQueryParameterEngineeringDto>();
        if (parameters.Count > MaximumParameterCount)
            problems.Add(new(
                "DATA_QUERY_PARAMETER_LIMIT",
                $"Data Query cannot declare more than {MaximumParameterCount} parameters."));
        var parameterMap = new Dictionary<string, DataQueryParameterEngineeringDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (parameter is null || string.IsNullOrWhiteSpace(parameter.Key))
            {
                problems.Add(new("DATA_QUERY_PARAMETER_KEY_REQUIRED", "Data Query parameter Key is required."));
                continue;
            }

            var key = parameter.Key.Trim();
            if (!parameterMap.TryAdd(key, parameter))
                problems.Add(new("DATA_QUERY_PARAMETER_KEY_DUPLICATE", $"Data Query parameter key '{key}' is duplicated."));
            if (string.IsNullOrWhiteSpace(parameter.Name))
                problems.Add(new("DATA_QUERY_PARAMETER_NAME_REQUIRED", $"Data Query parameter '{key}' requires a Name."));
            if (parameter.DefaultValue is not null && parameter.DefaultValue.Type != parameter.Type)
                problems.Add(new(
                    "DATA_QUERY_PARAMETER_DEFAULT_TYPE",
                    $"Default value type for parameter '{key}' does not match parameter type '{parameter.Type}'."));
            if (parameter.AllowedValues is not null &&
                parameter.AllowedValues.Any(value => value is null || value.Type != parameter.Type))
                problems.Add(new(
                    "DATA_QUERY_PARAMETER_ALLOWED_TYPE",
                    $"Allowed values for parameter '{key}' must all use type '{parameter.Type}'."));
        }

        var bindings = definition.ParameterBindings ?? Array.Empty<DataQueryParameterBindingEngineeringDto>();
        foreach (var binding in bindings)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.ParameterKey) ||
                !parameterMap.TryGetValue(binding.ParameterKey.Trim(), out var parameter))
            {
                problems.Add(new(
                    "DATA_QUERY_PARAMETER_BINDING_UNKNOWN",
                    $"Data Query parameter binding '{binding?.ParameterKey}' does not resolve a declared parameter."));
                continue;
            }

            var expected = binding.Target switch
            {
                DataQueryParameterTarget.AbsoluteFromUtc => DataQueryParameterType.DateTime,
                DataQueryParameterTarget.AbsoluteToUtc => DataQueryParameterType.DateTime,
                DataQueryParameterTarget.HistorianTargetUtc => DataQueryParameterType.DateTime,
                DataQueryParameterTarget.RelativeDurationSeconds => DataQueryParameterType.DurationSeconds,
                DataQueryParameterTarget.Search => DataQueryParameterType.String,
                DataQueryParameterTarget.FilterValue => parameter.Type,
                _ => parameter.Type
            };
            if (parameter.Type != expected)
                problems.Add(new(
                    "DATA_QUERY_PARAMETER_BINDING_TYPE",
                    $"Parameter '{parameter.Key}' type '{parameter.Type}' is incompatible with target '{binding.Target}'."));

            if (binding.Target == DataQueryParameterTarget.FilterValue &&
                (!binding.FilterIndex.HasValue || !binding.ValueIndex.HasValue))
                problems.Add(new(
                    "DATA_QUERY_FILTER_BINDING_INDEX_REQUIRED",
                    $"Filter-value parameter '{parameter.Key}' requires FilterIndex and ValueIndex."));
            if (binding.Target != DataQueryParameterTarget.FilterValue &&
                (binding.FilterIndex.HasValue || binding.ValueIndex.HasValue))
                problems.Add(new(
                    "DATA_QUERY_BINDING_INDEX_UNEXPECTED",
                    $"Parameter '{parameter.Key}' may specify filter indexes only for FilterValue bindings."));
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
        if (view.Filter is null)
            problems.Add(new("ALARM_VIEW_FILTER_REQUIRED", "Alarm View Filter is required."));
        if (view.Version != R2SharedEngineeringContractVersions.AlarmView)
            problems.Add(new(
                "ALARM_VIEW_VERSION_UNSUPPORTED",
                $"Alarm View version {view.Version} is unsupported; expected {R2SharedEngineeringContractVersions.AlarmView}."));

        if (view.Filter is not null)
        {
            if (view.Filter.Search?.Length > MaximumAlarmSearchLength)
                problems.Add(new(
                    "ALARM_VIEW_SEARCH_LIMIT",
                    $"Alarm View search cannot exceed {MaximumAlarmSearchLength} characters."));
            if ((view.Filter.AlarmIds ?? Array.Empty<Guid>()).Any(static id => id == Guid.Empty) ||
                (view.Filter.TagIds ?? Array.Empty<Guid>()).Any(static id => id == Guid.Empty) ||
                (view.Filter.EquipmentIds ?? Array.Empty<Guid>()).Any(static id => id == Guid.Empty))
                problems.Add(new("ALARM_VIEW_ID_FILTER_INVALID", "Alarm View identity filters cannot contain empty GUIDs."));
        }

        return problems;
    }

    private static void ValidateRetrieval(
        DataQueryEngineeringDto definition,
        IReadOnlyCollection<DataQueryParameterBindingEngineeringDto> bindings,
        HistoricalValidatedRequest? validated,
        List<DataQueryValidationProblem> problems)
    {
        var retrieval = definition.HistorianRetrieval;
        if (retrieval is null) return;

        if (validated is not null &&
            !string.Equals(validated.Dataset.Id, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal) &&
            retrieval.Mode != HistorianRetrievalMode.Raw)
        {
            problems.Add(new(
                "DATA_QUERY_RETRIEVAL_DATASET_UNSUPPORTED",
                $"Historian retrieval mode '{retrieval.Mode}' requires dataset '{HistoricalDatasets.HistorianSamples}'."));
        }

        var hasTargetBinding = bindings.Any(static binding =>
            binding.Target == DataQueryParameterTarget.HistorianTargetUtc);
        if (retrieval.Mode is HistorianRetrievalMode.AtOrBefore or
            HistorianRetrievalMode.AtOrAfter or
            HistorianRetrievalMode.Exact or
            HistorianRetrievalMode.Interpolated)
        {
            if (!retrieval.TargetUtc.HasValue && !hasTargetBinding)
                problems.Add(new(
                    "DATA_QUERY_TARGET_REQUIRED",
                    $"Historian retrieval mode '{retrieval.Mode}' requires TargetUtc or a historianTargetUtc parameter binding."));
            if (retrieval.TargetUtc is { Offset: var offset } && offset != TimeSpan.Zero)
                problems.Add(new("DATA_QUERY_TARGET_UTC_REQUIRED", "Historian retrieval TargetUtc must use UTC offset +00:00."));
        }

        if (retrieval.Mode is HistorianRetrievalMode.Interpolated or HistorianRetrievalMode.SampledFixedStep)
        {
            if (retrieval.MaximumGapMilliseconds is not > 0)
                problems.Add(new(
                    "DATA_QUERY_MAXIMUM_GAP_REQUIRED",
                    $"Historian retrieval mode '{retrieval.Mode}' requires a positive MaximumGapMilliseconds."));
        }

        if (retrieval.Mode == HistorianRetrievalMode.SampledFixedStep &&
            retrieval.StepMilliseconds is not > 0)
            problems.Add(new(
                "DATA_QUERY_STEP_REQUIRED",
                "SampledFixedStep retrieval requires a positive StepMilliseconds."));

        if (retrieval.Mode == HistorianRetrievalMode.Aggregate)
        {
            if (retrieval.BucketMilliseconds is not > 0)
                problems.Add(new(
                    "DATA_QUERY_BUCKET_REQUIRED",
                    "Aggregate retrieval requires a positive BucketMilliseconds."));
            if (!retrieval.AggregateFunction.HasValue &&
                (definition.Aggregates?.Count ?? 0) != 1)
                problems.Add(new(
                    "DATA_QUERY_AGGREGATE_FUNCTION_REQUIRED",
                    "Aggregate retrieval requires AggregateFunction or exactly one typed aggregate descriptor."));
        }
    }

    private static string FirstLine(string value)
    {
        var index = value.IndexOfAny(['\r', '\n']);
        return index < 0 ? value : value[..index];
    }
}

public interface IDataQueryEngineeringRegistry
{
    IReadOnlyCollection<DataQueryEngineeringDto> Snapshot();
    DataQueryEngineeringDto? Find(Guid id);
    DataQueryEngineeringDto? FindByKey(string key);
    DataQueryEngineeringDto Upsert(DataQueryEngineeringDto definition);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryDataQueryEngineeringRegistry(Action? changed = null) : IDataQueryEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, DataQueryEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<DataQueryEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(static value => value.Key, StringComparer.OrdinalIgnoreCase).ToArray();
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

    public DataQueryEngineeringDto Upsert(DataQueryEngineeringDto definition)
    {
        var problems = DataQueryEngineeringValidation.Validate(definition);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(static problem => problem.Message)), nameof(definition));

        var key = definition.Key.Trim();
        lock (_sync)
        {
            var existingByKey = _byKey.TryGetValue(key, out var keyId) ? _byId.GetValueOrDefault(keyId) : null;
            Guid id = definition.Id ?? existingByKey?.Id ?? Guid.NewGuid();
            if (id == Guid.Empty)
                throw new ArgumentException("Data Query Id cannot be empty.", nameof(definition));

            if (existingByKey?.Id is Guid existingKeyId && existingKeyId != id)
                throw new InvalidOperationException($"Data Query key '{key}' is already owned by another stable identity.");
            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                _byKey.TryGetValue(key, out var otherId) &&
                otherId != id)
                throw new InvalidOperationException($"Data Query key '{key}' is already owned by another stable identity.");

            if (_byId.TryGetValue(id, out previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            var normalized = definition with { Id = id, Key = key, Name = definition.Name.Trim(), ProviderKey = definition.ProviderKey.Trim() };
            _byId[id] = normalized;
            _byKey[key] = id;
            changed?.Invoke();
            return normalized;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_sync)
        {
            if (!_byId.Remove(id, out var removed)) return false;
            _byKey.Remove(removed.Key);
        }
        changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }
        changed?.Invoke();
    }
}

public interface IAlarmViewEngineeringRegistry
{
    IReadOnlyCollection<AlarmViewEngineeringDto> Snapshot();
    AlarmViewEngineeringDto? Find(Guid id);
    AlarmViewEngineeringDto? FindByKey(string key);
    AlarmViewEngineeringDto Upsert(AlarmViewEngineeringDto view);
    bool Remove(Guid id);
    void Clear();
}

public sealed class InMemoryAlarmViewEngineeringRegistry(Action? changed = null) : IAlarmViewEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, AlarmViewEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AlarmViewEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(static value => value.Key, StringComparer.OrdinalIgnoreCase).ToArray();
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

    public AlarmViewEngineeringDto Upsert(AlarmViewEngineeringDto view)
    {
        var problems = DataQueryEngineeringValidation.Validate(view);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(static problem => problem.Message)), nameof(view));

        var key = view.Key.Trim();
        lock (_sync)
        {
            var existingByKey = _byKey.TryGetValue(key, out var keyId) ? _byId.GetValueOrDefault(keyId) : null;
            Guid id = view.Id ?? existingByKey?.Id ?? Guid.NewGuid();
            if (id == Guid.Empty)
                throw new ArgumentException("Alarm View Id cannot be empty.", nameof(view));

            if (existingByKey?.Id is Guid existingKeyId && existingKeyId != id)
                throw new InvalidOperationException($"Alarm View key '{key}' is already owned by another stable identity.");
            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                _byKey.TryGetValue(key, out var otherId) &&
                otherId != id)
                throw new InvalidOperationException($"Alarm View key '{key}' is already owned by another stable identity.");

            if (_byId.TryGetValue(id, out previous) &&
                !previous.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            var normalized = view with { Id = id, Key = key, Name = view.Name.Trim() };
            _byId[id] = normalized;
            _byKey[key] = id;
            changed?.Invoke();
            return normalized;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_sync)
        {
            if (!_byId.Remove(id, out var removed)) return false;
            _byKey.Remove(removed.Key);
        }
        changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }
        changed?.Invoke();
    }
}

/// <summary>
/// Adds Data Query and Alarm View lifecycle semantics around the existing Engineering
/// exchange authority without redefining the F0 wire or mutating ImportExport-owned files.
/// </summary>
public sealed class DataQueryEngineeringExchangeDecorator(
    IEngineeringExchangeService inner,
    IDataQueryEngineeringRegistry queries,
    IAlarmViewEngineeringRegistry alarmViews) : IEngineeringExchangeService
{
    public EngineeringPackage ExportPackage() =>
        inner.ExportPackage() with
        {
            DataQueries = queries.Snapshot(),
            AlarmViews = alarmViews.Snapshot()
        };

    public string ExportJson(bool indented = true)
    {
        var root = JsonNode.Parse(inner.ExportJson(indented: false))?.AsObject()
            ?? throw new InvalidDataException("Engineering package JSON root is invalid.");
        root["dataQueries"] = JsonSerializer.SerializeToNode(queries.Snapshot());
        root["alarmViews"] = JsonSerializer.SerializeToNode(alarmViews.Snapshot());
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    public string ExportTagsCsv() => inner.ExportTagsCsv();
    public string ExportAlarmsCsv() => inner.ExportAlarmsCsv();
    public string ExportDataSourcesCsv() => inner.ExportDataSourcesCsv();
    public EngineeringPackage ParseJson(string json) => inner.ParseJson(json);
    public EngineeringPackage ParseTagsCsv(string csv) => inner.ParseTagsCsv(csv);
    public EngineeringPackage ParseAlarmsCsv(string csv) => inner.ParseAlarmsCsv(csv);
    public EngineeringPackage ParseDataSourcesCsv(string csv) => inner.ParseDataSourcesCsv(csv);

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode) =>
        Preview(package, mode, null);

    public ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var baseline = inner.Preview(package, mode, context);
        var items = baseline.Items.ToList();

        foreach (var definition in package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
            items.Add(PreviewQuery(definition, mode));
        foreach (var view in package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
            items.Add(PreviewAlarmView(view, mode));

        return new ImportPreview(
            mode,
            items.Count(static item => item.Operation == ImportOperation.Create),
            items.Count(static item => item.Operation == ImportOperation.Update),
            items.Count(static item => item.Operation == ImportOperation.Skip),
            items.Count(static item => item.Operation == ImportOperation.Error),
            items);
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
                preview.Items.SelectMany(static item => item.Issues).ToArray());

        var baseline = inner.Apply(package, mode, context);
        if (baseline.Issues.Any(static issue => issue.IsError))
            return baseline;

        var created = baseline.Created;
        var updated = baseline.Updated;
        var skipped = baseline.Skipped;

        foreach (var definition in package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
            ApplyQuery(definition, mode, ref created, ref updated, ref skipped);
        foreach (var view in package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
            ApplyAlarmView(view, mode, ref created, ref updated, ref skipped);

        return new ImportResult(mode, created, updated, skipped, baseline.Issues);
    }

    private ImportPreviewItem PreviewQuery(DataQueryEngineeringDto definition, ImportMode mode)
    {
        var problems = DataQueryEngineeringValidation.Validate(definition);
        if (problems.Count > 0)
            return ErrorItem(
                ImportEntityKind.DataQuery,
                definition.Key,
                problems.Select(static problem => new ImportIssue(
                    problem.Code,
                    problem.Message,
                    ImportEntityKind.DataQuery,
                    "data-query",
                    true)).ToArray());

        var existing = Resolve(queries, definition);
        if (existing.Conflict is not null)
            return ErrorItem(ImportEntityKind.DataQuery, definition.Key, [existing.Conflict]);

        return new ImportPreviewItem(
            ImportEntityKind.DataQuery,
            definition.Key,
            Operation(existing.Exists, mode),
            Array.Empty<ImportIssue>());
    }

    private ImportPreviewItem PreviewAlarmView(AlarmViewEngineeringDto view, ImportMode mode)
    {
        var problems = DataQueryEngineeringValidation.Validate(view);
        if (problems.Count > 0)
            return ErrorItem(
                ImportEntityKind.AlarmView,
                view.Key,
                problems.Select(static problem => new ImportIssue(
                    problem.Code,
                    problem.Message,
                    ImportEntityKind.AlarmView,
                    "alarm-view",
                    true)).ToArray());

        var existing = Resolve(alarmViews, view);
        if (existing.Conflict is not null)
            return ErrorItem(ImportEntityKind.AlarmView, view.Key, [existing.Conflict]);

        return new ImportPreviewItem(
            ImportEntityKind.AlarmView,
            view.Key,
            Operation(existing.Exists, mode),
            Array.Empty<ImportIssue>());
    }

    private void ApplyQuery(DataQueryEngineeringDto definition, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        var exists = Resolve(queries, definition).Exists;
        var operation = Operation(exists, mode);
        if (operation == ImportOperation.Skip) { skipped++; return; }
        queries.Upsert(definition);
        if (operation == ImportOperation.Create) created++; else updated++;
    }

    private void ApplyAlarmView(AlarmViewEngineeringDto view, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        var exists = Resolve(alarmViews, view).Exists;
        var operation = Operation(exists, mode);
        if (operation == ImportOperation.Skip) { skipped++; return; }
        alarmViews.Upsert(view);
        if (operation == ImportOperation.Create) created++; else updated++;
    }

    private static ImportOperation Operation(bool exists, ImportMode mode) => mode switch
    {
        ImportMode.CreateOnly => exists ? ImportOperation.Skip : ImportOperation.Create,
        ImportMode.UpdateExisting => exists ? ImportOperation.Update : ImportOperation.Skip,
        ImportMode.CreateAndUpdate => exists ? ImportOperation.Update : ImportOperation.Create,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static ImportPreviewItem ErrorItem(
        ImportEntityKind kind,
        string? key,
        IReadOnlyCollection<ImportIssue> issues) =>
        new(kind, string.IsNullOrWhiteSpace(key) ? "<invalid>" : key, ImportOperation.Error, issues);

    private static (bool Exists, ImportIssue? Conflict) Resolve(
        IDataQueryEngineeringRegistry registry,
        DataQueryEngineeringDto definition)
    {
        var byId = definition.Id.HasValue ? registry.Find(definition.Id.Value) : null;
        var byKey = registry.FindByKey(definition.Key);
        if (byId is not null && byKey is not null && byId.Id != byKey.Id)
            return (true, new ImportIssue(
                "DATA_QUERY_ID_KEY_CONFLICT",
                $"Data Query Id and key '{definition.Key}' resolve different canonical definitions.",
                ImportEntityKind.DataQuery,
                definition.Key,
                true));
        if (byKey is not null && definition.Id.HasValue && byKey.Id != definition.Id)
            return (true, new ImportIssue(
                "DATA_QUERY_KEY_CONFLICT",
                $"Data Query key '{definition.Key}' is already owned by another stable identity.",
                ImportEntityKind.DataQuery,
                definition.Key,
                true));
        return (byId is not null || byKey is not null, null);
    }

    private static (bool Exists, ImportIssue? Conflict) Resolve(
        IAlarmViewEngineeringRegistry registry,
        AlarmViewEngineeringDto view)
    {
        var byId = view.Id.HasValue ? registry.Find(view.Id.Value) : null;
        var byKey = registry.FindByKey(view.Key);
        if (byId is not null && byKey is not null && byId.Id != byKey.Id)
            return (true, new ImportIssue(
                "ALARM_VIEW_ID_KEY_CONFLICT",
                $"Alarm View Id and key '{view.Key}' resolve different canonical definitions.",
                ImportEntityKind.AlarmView,
                view.Key,
                true));
        if (byKey is not null && view.Id.HasValue && byKey.Id != view.Id)
            return (true, new ImportIssue(
                "ALARM_VIEW_KEY_CONFLICT",
                $"Alarm View key '{view.Key}' is already owned by another stable identity.",
                ImportEntityKind.AlarmView,
                view.Key,
                true));
        return (byId is not null || byKey is not null, null);
    }
}
