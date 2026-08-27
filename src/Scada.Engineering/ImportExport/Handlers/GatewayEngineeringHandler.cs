using System.Security.Cryptography;
using System.Text;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class GatewayEngineeringHandler
{
    private readonly IGatewayEngineeringRegistry _registry;
    private readonly ITagRegistry _tags;
    private readonly IDataSourceEngineeringRegistry _dataSources;

    public GatewayEngineeringHandler(
        IGatewayEngineeringRegistry registry,
        ITagRegistry tags,
        IDataSourceEngineeringRegistry dataSources)
    {
        _registry = registry;
        _tags = tags;
        _dataSources = dataSources;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var incoming = (package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>()).ToArray();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(incoming.Select(x => x.Key));
        var effectiveTags = BuildEffectiveTags(package, mode);
        var effectiveDataSources = BuildEffectiveDataSources(package, mode);
        var (effectiveRoutes, incomingEffectiveIds) = BuildEffectiveRoutes(incoming, mode);

        var validations = effectiveRoutes.ToDictionary(
            pair => pair.Key,
            pair => ValidateEffectiveRoute(pair.Value, effectiveTags, effectiveDataSources));

        AddMultiWriterIssues(validations);
        AddCycleIssues(validations);

        for (var index = 0; index < incoming.Length; index++)
        {
            var route = incoming[index];
            var existing = ResolveExisting(route);
            var issues = incomingEffectiveIds.TryGetValue(index, out var effectiveId) && validations.TryGetValue(effectiveId, out var validation)
                ? validation.Issues.ToList()
                : GatewayEngineeringValidator.Validate(route).ToList();

            if (duplicateKeys.Contains(route.Key))
            {
                AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                    "GATEWAY_DUPLICATE_IN_FILE",
                    $"Gateway route key '{route.Key}' appears more than once in the import package.",
                    route.Key));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Gateway,
                route.Key,
                existing is not null,
                mode,
                issues);
        }

        // Existing routes also depend on TAG/Data Source definitions. If this import
        // changes an endpoint so an already-engineered route would become unsafe,
        // surface a synthetic Gateway error and fail the whole preview before any
        // partial Apply can mutate the workspace.
        var represented = incomingEffectiveIds.Values.ToHashSet();
        foreach (var pair in validations.Where(pair => !represented.Contains(pair.Key) && pair.Value.Issues.Any(x => x.IsError)))
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Gateway,
                pair.Value.Route.Key,
                ImportOperation.Error,
                pair.Value.Issues));
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var route in package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
        {
            var existing = ResolveExisting(route);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _registry.Upsert(route with { Id = existing?.Id ?? route.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private GatewayRouteEngineeringDto? ResolveExisting(GatewayRouteEngineeringDto route)
    {
        if (route.Id.HasValue)
        {
            var byId = _registry.Find(route.Id.Value);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(route.Key) ? null : _registry.FindByKey(route.Key);
    }

    private (Dictionary<Guid, GatewayRouteEngineeringDto> Routes, Dictionary<int, Guid> IncomingIds) BuildEffectiveRoutes(
        IReadOnlyList<GatewayRouteEngineeringDto> incoming,
        ImportMode mode)
    {
        var routes = new Dictionary<Guid, GatewayRouteEngineeringDto>();
        var byKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var current in _registry.Snapshot())
        {
            var id = current.Id ?? DeterministicTemporaryId($"gateway:{current.Key}");
            var normalized = current with { Id = id };
            routes[id] = normalized;
            if (!string.IsNullOrWhiteSpace(normalized.Key)) byKey[normalized.Key] = id;
        }

        var incomingIds = new Dictionary<int, Guid>();
        for (var index = 0; index < incoming.Count; index++)
        {
            var route = incoming[index];
            GatewayRouteEngineeringDto? existing = null;
            Guid? existingId = null;

            if (route.Id.HasValue && routes.TryGetValue(route.Id.Value, out var byId))
            {
                existing = byId;
                existingId = route.Id.Value;
            }
            else if (!string.IsNullOrWhiteSpace(route.Key) && byKey.TryGetValue(route.Key, out var keyedId))
            {
                existing = routes[keyedId];
                existingId = keyedId;
            }

            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                if (existingId.HasValue) incomingIds[index] = existingId.Value;
                continue;
            }

            var id = existingId ?? route.Id ?? DeterministicTemporaryId($"gateway:{route.Key}:{index}");
            if (existingId.HasValue && routes.TryGetValue(existingId.Value, out var previous) && !string.IsNullOrWhiteSpace(previous.Key))
                byKey.Remove(previous.Key);

            var normalized = route with { Id = id };
            routes[id] = normalized;
            if (!string.IsNullOrWhiteSpace(route.Key)) byKey[route.Key] = id;
            incomingIds[index] = id;
        }

        return (routes, incomingIds);
    }

    private EffectiveTagCatalog BuildEffectiveTags(EngineeringPackage package, ImportMode mode)
    {
        var catalog = new EffectiveTagCatalog();
        foreach (var tag in _tags.Snapshot())
            catalog.Upsert(new TagTarget(tag.Id, tag.Path, tag.DataType, tag.Source, tag.ReadOnly));

        foreach (var dto in package.Tags)
        {
            TagDefinition? existing = null;
            if (dto.Id.HasValue && _tags.TryGet(dto.Id.Value, out var byId)) existing = byId;
            if (existing is null && _tags.TryGetByPath(dto.Path, out var byPath)) existing = byPath;

            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) continue;

            var id = existing?.Id ?? dto.Id ?? DeterministicTemporaryId($"tag:{dto.Path}");
            if (existing is not null) catalog.Remove(existing.Id);
            catalog.Upsert(new TagTarget(id, dto.Path, dto.DataType, dto.Source, dto.ReadOnly));
        }

        return catalog;
    }

    private EffectiveDataSourceCatalog BuildEffectiveDataSources(EngineeringPackage package, ImportMode mode)
    {
        var catalog = new EffectiveDataSourceCatalog();
        foreach (var dataSource in _dataSources.Snapshot())
        {
            var id = dataSource.Id ?? DeterministicTemporaryId($"datasource:{dataSource.Key}");
            catalog.Upsert(new DataSourceTarget(id, dataSource.Key, dataSource.Driver, dataSource.Enabled));
        }

        foreach (var dto in package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
        {
            var existing = dto.Id.HasValue ? _dataSources.Find(dto.Id.Value) : null;
            existing ??= _dataSources.FindByKey(dto.Key);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) continue;

            var id = existing?.Id ?? dto.Id ?? DeterministicTemporaryId($"datasource:{dto.Key}");
            if (existing?.Id is Guid existingId) catalog.Remove(existingId);
            catalog.Upsert(new DataSourceTarget(id, dto.Key, dto.Driver, dto.Enabled));
        }

        return catalog;
    }

    private RouteValidation ValidateEffectiveRoute(
        GatewayRouteEngineeringDto route,
        EffectiveTagCatalog tags,
        EffectiveDataSourceCatalog dataSources)
    {
        var issues = GatewayEngineeringValidator.Validate(route).ToList();
        var source = ResolveEndpoint(route, true, tags, issues);
        var destination = ResolveEndpoint(route, false, tags, issues);

        if (source is not null)
            ValidateEndpointDataSource(route, source, true, dataSources, issues);
        if (destination is not null)
            ValidateEndpointDataSource(route, destination, false, dataSources, issues);

        if (source is not null && destination is not null)
        {
            if (source.Id == destination.Id)
            {
                AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                    "GATEWAY_SELF_ROUTE_NOT_ALLOWED",
                    $"Gateway route '{route.Key}' cannot use the same TAG '{source.Path}' as source and destination.",
                    route.Key));
            }

            if (destination.ReadOnly)
            {
                AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                    "GATEWAY_DESTINATION_READ_ONLY",
                    $"Gateway route '{route.Key}' targets read-only TAG '{destination.Path}'.",
                    route.Key));
            }

            switch (route.ConversionPolicy)
            {
                case GatewayConversionPolicy.Exact when source.DataType != destination.DataType:
                    AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                        "GATEWAY_EXACT_TYPE_MISMATCH",
                        $"Gateway route '{route.Key}' requires identical source/destination types under Exact conversion, but found {source.DataType} -> {destination.DataType}.",
                        route.Key));
                    break;

                case GatewayConversionPolicy.CheckedNumeric:
                    if (!GatewayEngineeringValidator.IsNumeric(source.DataType) || !GatewayEngineeringValidator.IsNumeric(destination.DataType))
                    {
                        AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                            "GATEWAY_NUMERIC_CONVERSION_REQUIRES_NUMERIC_TYPES",
                            $"Gateway route '{route.Key}' CheckedNumeric conversion requires numeric source and destination TAG types.",
                            route.Key));
                    }
                    break;
            }

            if (route.Deadband is not null && !GatewayEngineeringValidator.IsNumeric(source.DataType))
            {
                AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                    "GATEWAY_DEADBAND_REQUIRES_NUMERIC_SOURCE",
                    $"Gateway route '{route.Key}' deadband requires a numeric source TAG.",
                    route.Key));
            }
        }

        return new RouteValidation(route, source, destination, issues);
    }

    private static TagTarget? ResolveEndpoint(
        GatewayRouteEngineeringDto route,
        bool source,
        EffectiveTagCatalog tags,
        List<ImportIssue> issues)
    {
        var id = source ? route.SourceTagId : route.DestinationTagId;
        var path = source ? route.SourceTagPath : route.DestinationTagPath;
        var label = source ? "source" : "destination";
        var prefix = source ? "GATEWAY_SOURCE" : "GATEWAY_DESTINATION";

        var byId = id.HasValue ? tags.Find(id.Value) : null;
        var byPath = !string.IsNullOrWhiteSpace(path) ? tags.FindByPath(path) : null;

        if (id.HasValue && byId is null)
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_TAG_NOT_FOUND",
                $"Gateway route '{route.Key}' {label} TAG ID '{id}' was not found in the effective Engineering workspace.",
                route.Key));
        }

        if (!string.IsNullOrWhiteSpace(path) && byPath is null)
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_TAG_NOT_FOUND",
                $"Gateway route '{route.Key}' {label} TAG path '{path}' was not found in the effective Engineering workspace.",
                route.Key));
        }

        if (byId is not null && !string.IsNullOrWhiteSpace(path) && !byId.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_TAG_MISMATCH",
                $"Gateway route '{route.Key}' {label} TAG ID resolves to '{byId.Path}', not supplied path '{path}'.",
                route.Key));
            return null;
        }

        if (byId is not null && byPath is not null && byId.Id != byPath.Id)
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_TAG_MISMATCH",
                $"Gateway route '{route.Key}' {label} TAG ID and path resolve to different TAGs.",
                route.Key));
            return null;
        }

        return byId ?? byPath;
    }

    private static void ValidateEndpointDataSource(
        GatewayRouteEngineeringDto route,
        TagTarget target,
        bool source,
        EffectiveDataSourceCatalog dataSources,
        List<ImportIssue> issues)
    {
        var label = source ? "source" : "destination";
        var prefix = source ? "GATEWAY_SOURCE" : "GATEWAY_DESTINATION";
        var dataSource = string.IsNullOrWhiteSpace(target.Source) ? null : dataSources.FindByKey(target.Source);

        if (MemoryEngineeringValidator.IsClientMemoryDriver(dataSource?.Driver) || MemoryEngineeringValidator.IsClientMemoryDriver(target.Source))
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_CLIENT_MEMORY_NOT_ALLOWED",
                $"Gateway route '{route.Key}' cannot use Client Memory TAG '{target.Path}' as a server gateway {label}.",
                route.Key));
        }

        if (dataSource is { Enabled: false })
        {
            AddIssueOnce(issues, GatewayEngineeringValidator.Error(
                $"{prefix}_DATASOURCE_DISABLED",
                $"Gateway route '{route.Key}' {label} TAG '{target.Path}' belongs to disabled Data Source '{dataSource.Key}'.",
                route.Key));
        }
    }

    private static void AddMultiWriterIssues(Dictionary<Guid, RouteValidation> validations)
    {
        var active = validations
            .Where(pair => pair.Value.Route.Enabled && pair.Value.Destination is not null)
            .GroupBy(pair => pair.Value.Destination!.Id)
            .Where(group => group.Count() > 1);

        foreach (var group in active)
        {
            var destinationPath = group.First().Value.Destination!.Path;
            foreach (var pair in group)
            {
                AddIssueOnce(pair.Value.Issues, GatewayEngineeringValidator.Error(
                    "GATEWAY_DESTINATION_MULTI_WRITER",
                    $"Gateway route '{pair.Value.Route.Key}' conflicts with another active route writing destination TAG '{destinationPath}'.",
                    pair.Value.Route.Key));
            }
        }
    }

    private static void AddCycleIssues(Dictionary<Guid, RouteValidation> validations)
    {
        var edges = validations
            .Where(pair => pair.Value.Route.Enabled && pair.Value.Source is not null && pair.Value.Destination is not null)
            .Select(pair => new RouteEdge(pair.Key, pair.Value.Source!.Id, pair.Value.Destination!.Id))
            .Where(edge => edge.SourceId != edge.DestinationId)
            .ToArray();

        var adjacency = edges
            .GroupBy(edge => edge.SourceId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.DestinationId).Distinct().ToArray());

        foreach (var edge in edges)
        {
            if (!CanReach(edge.DestinationId, edge.SourceId, adjacency, new HashSet<Guid>())) continue;
            var validation = validations[edge.RouteId];
            AddIssueOnce(validation.Issues, GatewayEngineeringValidator.Error(
                "GATEWAY_CYCLE_DETECTED",
                $"Gateway route '{validation.Route.Key}' participates in an active TAG routing cycle.",
                validation.Route.Key));
        }
    }

    private static bool CanReach(
        Guid current,
        Guid target,
        IReadOnlyDictionary<Guid, Guid[]> adjacency,
        HashSet<Guid> visited)
    {
        if (current == target) return true;
        if (!visited.Add(current) || !adjacency.TryGetValue(current, out var next)) return false;
        return next.Any(node => CanReach(node, target, adjacency, visited));
    }

    private static void AddIssueOnce(List<ImportIssue> issues, ImportIssue issue)
    {
        if (!issues.Any(existing => existing.Code.Equals(issue.Code, StringComparison.Ordinal)))
            issues.Add(issue);
    }

    private static Guid DeterministicTemporaryId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record RouteValidation(
        GatewayRouteEngineeringDto Route,
        TagTarget? Source,
        TagTarget? Destination,
        List<ImportIssue> Issues);

    private sealed record RouteEdge(Guid RouteId, Guid SourceId, Guid DestinationId);

    private sealed record TagTarget(
        Guid Id,
        string Path,
        TagDataType DataType,
        string? Source,
        bool ReadOnly);

    private sealed record DataSourceTarget(Guid Id, string Key, string Driver, bool Enabled);

    private sealed class EffectiveTagCatalog
    {
        private readonly Dictionary<Guid, TagTarget> _byId = new();
        private readonly Dictionary<string, Guid> _byPath = new(StringComparer.OrdinalIgnoreCase);

        public TagTarget? Find(Guid id) => _byId.GetValueOrDefault(id);
        public TagTarget? FindByPath(string path) => _byPath.TryGetValue(path, out var id) ? Find(id) : null;

        public void Upsert(TagTarget tag)
        {
            if (_byId.TryGetValue(tag.Id, out var previous)) _byPath.Remove(previous.Path);
            if (_byPath.TryGetValue(tag.Path, out var otherId) && otherId != tag.Id) _byId.Remove(otherId);
            _byId[tag.Id] = tag;
            _byPath[tag.Path] = tag.Id;
        }

        public void Remove(Guid id)
        {
            if (!_byId.Remove(id, out var removed)) return;
            _byPath.Remove(removed.Path);
        }
    }

    private sealed class EffectiveDataSourceCatalog
    {
        private readonly Dictionary<Guid, DataSourceTarget> _byId = new();
        private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

        public DataSourceTarget? FindByKey(string key) => _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;

        public void Upsert(DataSourceTarget dataSource)
        {
            if (_byId.TryGetValue(dataSource.Id, out var previous)) _byKey.Remove(previous.Key);
            if (_byKey.TryGetValue(dataSource.Key, out var otherId) && otherId != dataSource.Id) _byId.Remove(otherId);
            _byId[dataSource.Id] = dataSource;
            _byKey[dataSource.Key] = dataSource.Id;
        }

        public void Remove(Guid id)
        {
            if (!_byId.Remove(id, out var removed)) return;
            _byKey.Remove(removed.Key);
        }
    }
}