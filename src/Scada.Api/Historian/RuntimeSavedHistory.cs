using Scada.Api.Reports;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;

namespace Scada.Api.Historian;

internal static class RuntimeSavedHistory
{
    public static void MapRuntimeSavedHistory(this WebApplication app)
    {
        var group = app.MapGroup("/api/runtime/saved-history");
        group.MapGet("/", async (RuntimeReportCatalog catalog, IHistoricalQueryAuthorizer authorization, CancellationToken ct) =>
        {
            var package = await catalog.ActivePackageAsync(ct);
            var queries = new List<object>();
            foreach (var query in package?.DataQueries ?? [])
            {
                if ((await authorization.AuthorizeAsync(query.Query.Dataset, ct)).Outcome == HistoricalAuthorizationOutcome.Allowed)
                    queries.Add(new { query.Key, query.Name, query.Parameters });
            }
            var alarmAccess = await authorization.AuthorizeAsync(HistoricalDatasets.AlarmEvents, ct);
            var views = alarmAccess.Outcome == HistoricalAuthorizationOutcome.Allowed
                ? (package?.AlarmViews ?? []).Select(view => new { view.Key, view.Name }).ToArray()
                : [];
            return Results.Ok(new { queries, views });
        });

        group.MapPost("/query/{key}", async (
            string key, SavedQueryRequest request, RuntimeReportCatalog catalog,
            ITransientDataQueryExecutionService service, ILoggerFactory logs, CancellationToken ct) =>
        {
            var package = await catalog.ActivePackageAsync(ct);
            var definition = package?.DataQueries?.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (definition is null) return Results.NotFound();
            if (request.TimeRange is not null)
                definition = definition with { Query = definition.Query with { Range = request.TimeRange } };
            var page = request.Cursor is null ? null : (definition.Query.Page ?? new HistoricalPageRequest()) with { Cursor = request.Cursor };
            return await HistoricalQueryApi.ExecuteTransientDataQueryAsync(
                new(definition, new(request.Parameters, page)), service, ct,
                logs.CreateLogger(typeof(RuntimeSavedHistory).FullName!));
        });

        group.MapPost("/alarms/{key}", async (
            string key, HistoricalQueryRequest request, RuntimeReportCatalog catalog,
            IHistoricalQueryService service, ILoggerFactory logs, CancellationToken ct) =>
        {
            if (request.Dataset != HistoricalDatasets.AlarmEvents)
                return Results.BadRequest(new { error = "Alarm Views require the alarm.events dataset." });
            var package = await catalog.ActivePackageAsync(ct);
            var view = FindAlarmView(package, key);
            if (view is null) return Results.NotFound();
            return await HistoricalQueryApi.ExecuteAsync(
                request with { AlarmView = HistoricalScope(view.Filter, package!.Alarms) }, service, ct,
                logs.CreateLogger(typeof(RuntimeSavedHistory).FullName!));
        });
    }

    public static AlarmViewEngineeringDto? FindAlarmView(EngineeringPackage? package, string key) =>
        package?.AlarmViews?.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static AlarmViewFacts AlarmFacts(AlarmInstance alarm, AlarmDefinition? definition) =>
        AlarmViewFacts.FromInstance(alarm, definition?.AlarmClass) with
        {
            Category = definition?.Metadata?.GetValueOrDefault("category"),
            Subcondition = definition?.Metadata?.GetValueOrDefault("subcondition"),
            Source = definition?.Metadata?.GetValueOrDefault("source"),
            EquipmentId = definition?.EquipmentId
        };

    private static HistoricalAlarmViewScope HistoricalScope(AlarmViewFilterEngineeringDto filter, IEnumerable<AlarmEngineeringDto> definitions)
    {
        // Area, type and related metadata belong to the Active alarm definition.
        // Historical priority, state, TAG and message come from each stored event.
        var facts = definitions.Where(item => item.Id.HasValue).Select(item =>
            new AlarmViewFacts(item.Id!.Value, item.TagId ?? Guid.Empty, item.Priority, item.Type, AlarmState.Normal,
                item.Area, item.AlarmClass, item.Metadata?.GetValueOrDefault("category"),
                item.Metadata?.GetValueOrDefault("subcondition"), item.Metadata?.GetValueOrDefault("source"),
                item.EquipmentId)).ToArray();
        var metadata = filter with
        {
            AlarmIds = null, TagIds = null, Priorities = null, Search = null,
            Active = AlarmViewMatchState.Any, Acknowledged = AlarmViewMatchState.Any, Shelved = AlarmViewMatchState.Any
        };
        var hasMetadata = metadata.Areas is { Count: > 0 } || metadata.Types is { Count: > 0 } ||
            metadata.AlarmClasses is { Count: > 0 } || metadata.Categories is { Count: > 0 } ||
            metadata.Subconditions is { Count: > 0 } || metadata.Sources is { Count: > 0 } ||
            metadata.EquipmentIds is { Count: > 0 };
        var alarmIds = filter.AlarmIds is { Count: > 0 } explicitIds ? explicitIds.AsEnumerable() : null;
        if (hasMetadata)
        {
            var matching = facts.Where(fact => AlarmViewMatcher.Matches(metadata, fact)).Select(fact => fact.AlarmId);
            alarmIds = alarmIds is null ? matching : alarmIds.Intersect(matching);
        }
        var stateFilter = new AlarmViewFilterEngineeringDto(Active: filter.Active,
            Acknowledged: filter.Acknowledged, Shelved: filter.Shelved);
        var prototype = new AlarmViewFacts(Guid.Empty, Guid.Empty, default, default, default);
        var states = Enum.GetValues<AlarmState>()
            .Where(state => AlarmViewMatcher.Matches(stateFilter, prototype with { State = state }))
            .Select(state => (short)state).ToArray();
        var term = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var searchFilter = new AlarmViewFilterEngineeringDto(Search: term);
        return new(
            alarmIds?.Distinct().Order().ToArray(),
            filter.TagIds is { Count: > 0 } tags ? tags.Distinct().Order().ToArray() : null,
            filter.Priorities is { Count: > 0 } priorities ? priorities.Select(value => (short)value).Distinct().Order().ToArray() : null,
            states, term,
            term is null ? null : facts.Where(fact => AlarmViewMatcher.Matches(searchFilter, fact))
                .Select(fact => fact.AlarmId).Distinct().Order().ToArray());
    }

    internal sealed record SavedQueryRequest(
        HistoricalTimeRange? TimeRange, string? Cursor,
        IReadOnlyDictionary<string, DataQueryParameterValue>? Parameters = null);
}
