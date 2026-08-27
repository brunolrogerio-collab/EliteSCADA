using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;

namespace Scada.DriverHost.Runtime;

public enum GatewayRouteRuntimeState
{
    Stopped,
    WaitingForSource,
    Running,
    Degraded
}

public sealed record GatewayRouteRuntimeDiagnostic(
    Guid RouteId,
    string Key,
    string Name,
    bool Enabled,
    GatewayRouteRuntimeState State,
    Guid SourceTagId,
    string SourceTagPath,
    string? SourceDataSource,
    Guid DestinationTagId,
    string DestinationTagPath,
    string? DestinationDataSource,
    DateTimeOffset? LastSourceUpdateAtUtc,
    DateTimeOffset? LastSuccessfulTransferAtUtc,
    DateTimeOffset? LastFailedTransferAtUtc,
    long TransferCount,
    long SkippedTransferCount,
    long CoalescedUpdateCount,
    long WriteFailureCount,
    int ConsecutiveFailures,
    string? LastError,
    bool HasPendingValue,
    GatewayTransferMode TransferMode,
    int? EffectiveIntervalMilliseconds);

internal sealed class GatewayRuntimeEngine : IAsyncDisposable
{
    private readonly IScadaEventBus _eventBus;
    private readonly ICurrentTagCache _cache;
    private readonly Func<Guid, object?, CancellationToken, ValueTask> _writeAsync;
    private readonly RouteRuntime[] _routes;
    private readonly IReadOnlyDictionary<Guid, RouteRuntime[]> _onChangeBySourceTagId;
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _lifetime;
    private IDisposable? _subscription;
    private Task[] _periodicTasks = Array.Empty<Task>();
    private bool _started;

    private GatewayRuntimeEngine(
        IScadaEventBus eventBus,
        ICurrentTagCache cache,
        Func<Guid, object?, CancellationToken, ValueTask> writeAsync,
        IReadOnlyCollection<ResolvedGatewayRoute> routes)
    {
        _eventBus = eventBus;
        _cache = cache;
        _writeAsync = writeAsync;
        _routes = routes.Select(route => new RouteRuntime(this, route)).ToArray();
        _onChangeBySourceTagId = _routes
            .Where(route => route.Route.TransferMode == GatewayTransferMode.OnChange)
            .GroupBy(route => route.Source.Id)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    public static GatewayRuntimeEngine Create(
        IReadOnlyCollection<GatewayRouteEngineeringDto> routes,
        ITagRegistry registry,
        ICurrentTagCache cache,
        IScadaEventBus eventBus,
        Func<Guid, bool> canWrite,
        Func<Guid, object?, CancellationToken, ValueTask> writeAsync,
        List<RuntimeActivationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(canWrite);
        ArgumentNullException.ThrowIfNull(writeAsync);
        ArgumentNullException.ThrowIfNull(issues);

        var resolved = new List<ResolvedGatewayRoute>();
        var routeIds = new HashSet<Guid>();
        var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in routes)
        {
            foreach (var issue in GatewayEngineeringValidator.Validate(route).Where(issue => issue.IsError))
            {
                issues.Add(new RuntimeActivationIssue(
                    $"RUNTIME_{issue.Code}",
                    issue.Message,
                    route.Key));
            }

            if (!route.Enabled)
                continue;

            if (route.Id is not Guid routeId || routeId == Guid.Empty)
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_ID_REQUIRED",
                    $"Active Gateway route '{route.Key}' requires a stable non-empty route ID.",
                    route.Key));
                continue;
            }

            if (!routeIds.Add(routeId))
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_DUPLICATE_ID",
                    $"Active Gateway route ID '{routeId}' is duplicated.",
                    route.Key));
                continue;
            }

            if (!routeKeys.Add(route.Key))
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_DUPLICATE_KEY",
                    $"Active Gateway route key '{route.Key}' is duplicated.",
                    route.Key));
                continue;
            }

            var source = ResolveEndpoint(route, source: true, registry, issues);
            var destination = ResolveEndpoint(route, source: false, registry, issues);
            if (source is null || destination is null)
                continue;

            if (source.Id == destination.Id)
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_SELF_ROUTE_NOT_ALLOWED",
                    $"Gateway route '{route.Key}' cannot use TAG '{source.Path}' as both source and destination.",
                    route.Key));
                continue;
            }

            if (destination.ReadOnly)
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_DESTINATION_READ_ONLY",
                    $"Gateway route '{route.Key}' targets read-only TAG '{destination.Path}'.",
                    route.Key));
                continue;
            }

            if (!canWrite(destination.Id))
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_DESTINATION_NOT_WRITABLE",
                    $"Gateway route '{route.Key}' has no active writable provider for destination TAG '{destination.Path}'.",
                    route.Key));
                continue;
            }

            if (!ValidateTypeCompatibility(route, source, destination, issues))
                continue;

            resolved.Add(new ResolvedGatewayRoute(routeId, route, source, destination));
        }

        AddMultipleWriterIssues(resolved, issues);
        AddCycleIssues(resolved, issues);

        return new GatewayRuntimeEngine(eventBus, cache, writeAsync, resolved);
    }

    public IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> Diagnostics() =>
        _routes.Select(route => route.Snapshot()).OrderBy(route => route.Key, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_started) return;
            _started = true;
            _lifetime = new CancellationTokenSource();
            var token = _lifetime.Token;
            _subscription = _eventBus.Subscribe<TagValueChanged>(OnTagValueChangedAsync);

            foreach (var route in _routes)
            {
                route.MarkStarted();
                if (route.Route.InitialTransferPolicy == GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue &&
                    _cache.TryGet(route.Source.Id, out var current) && current is not null)
                {
                    route.QueueLatest(current, token);
                }
            }

            _periodicTasks = _routes
                .Where(route => route.Route.TransferMode == GatewayTransferMode.Periodic)
                .Select(route => Task.Run(() => PeriodicLoopAsync(route, token), CancellationToken.None))
                .ToArray();
        }
    }

    public async ValueTask StopAsync()
    {
        CancellationTokenSource? lifetime;
        IDisposable? subscription;
        Task[] periodic;
        Task[] workers;

        lock (_lifecycleGate)
        {
            if (!_started)
            {
                foreach (var route in _routes) route.MarkStopped();
                return;
            }

            _started = false;
            lifetime = _lifetime;
            subscription = _subscription;
            periodic = _periodicTasks;
            workers = _routes.Select(route => route.WorkerTask).Where(task => task is not null).Cast<Task>().ToArray();
            _lifetime = null;
            _subscription = null;
            _periodicTasks = Array.Empty<Task>();
        }

        subscription?.Dispose();
        lifetime?.Cancel();

        try
        {
            await Task.WhenAll(periodic.Concat(workers));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            lifetime?.Dispose();
            foreach (var route in _routes) route.MarkStopped();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private ValueTask OnTagValueChangedAsync(TagValueChanged change)
    {
        if (_lifetime is not { IsCancellationRequested: false } lifetime)
            return ValueTask.CompletedTask;

        if (_onChangeBySourceTagId.TryGetValue(change.Tag.Id, out var routes))
        {
            foreach (var route in routes)
                route.QueueLatest(change.Current, lifetime.Token);
        }

        return ValueTask.CompletedTask;
    }

    private async Task PeriodicLoopAsync(RouteRuntime route, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(route.Route.PeriodMilliseconds!.Value);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);
                if (_cache.TryGet(route.Source.Id, out var current) && current is not null)
                    await route.TransferLatestAsync(current, applyDeadband: false, cancellationToken);
                else
                    route.RecordNoSourceValue();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask WriteDestinationAsync(
        RouteRuntime route,
        TagValue sourceValue,
        CancellationToken cancellationToken)
    {
        var converted = ConvertValue(route.Route, route.Source.DataType, route.Destination.DataType, sourceValue.Value);
        await _writeAsync(route.Destination.Id, converted, cancellationToken);
    }

    private static TagDefinition? ResolveEndpoint(
        GatewayRouteEngineeringDto route,
        bool source,
        ITagRegistry registry,
        List<RuntimeActivationIssue> issues)
    {
        var id = source ? route.SourceTagId : route.DestinationTagId;
        var path = source ? route.SourceTagPath : route.DestinationTagPath;
        var label = source ? "source" : "destination";
        var prefix = source ? "RUNTIME_GATEWAY_SOURCE" : "RUNTIME_GATEWAY_DESTINATION";

        TagDefinition? byId = null;
        TagDefinition? byPath = null;
        if (id.HasValue)
            registry.TryGet(id.Value, out byId);
        if (!string.IsNullOrWhiteSpace(path))
            registry.TryGetByPath(path, out byPath);

        if (id.HasValue && byId is null)
            issues.Add(new RuntimeActivationIssue($"{prefix}_TAG_NOT_ACTIVE", $"Gateway route '{route.Key}' {label} TAG ID '{id}' is not active.", route.Key));
        if (!string.IsNullOrWhiteSpace(path) && byPath is null)
            issues.Add(new RuntimeActivationIssue($"{prefix}_TAG_NOT_ACTIVE", $"Gateway route '{route.Key}' {label} TAG path '{path}' is not active.", route.Key));

        if (byId is not null && !string.IsNullOrWhiteSpace(path) && !byId.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RuntimeActivationIssue(
                $"{prefix}_TAG_MISMATCH",
                $"Gateway route '{route.Key}' {label} TAG ID resolves to '{byId.Path}', not supplied path '{path}'.",
                route.Key));
            return null;
        }

        if (byId is not null && byPath is not null && byId.Id != byPath.Id)
        {
            issues.Add(new RuntimeActivationIssue(
                $"{prefix}_TAG_MISMATCH",
                $"Gateway route '{route.Key}' {label} TAG ID and path resolve to different active TAGs.",
                route.Key));
            return null;
        }

        return byId ?? byPath;
    }

    private static bool ValidateTypeCompatibility(
        GatewayRouteEngineeringDto route,
        TagDefinition source,
        TagDefinition destination,
        List<RuntimeActivationIssue> issues)
    {
        if (route.ConversionPolicy == GatewayConversionPolicy.Exact)
        {
            if (source.DataType == destination.DataType) return true;
            issues.Add(new RuntimeActivationIssue(
                "RUNTIME_GATEWAY_EXACT_TYPE_MISMATCH",
                $"Gateway route '{route.Key}' requires identical types but found {source.DataType} -> {destination.DataType}.",
                route.Key));
            return false;
        }

        if (route.ConversionPolicy == GatewayConversionPolicy.CheckedNumeric &&
            GatewayEngineeringValidator.IsNumeric(source.DataType) &&
            GatewayEngineeringValidator.IsNumeric(destination.DataType))
            return true;

        issues.Add(new RuntimeActivationIssue(
            "RUNTIME_GATEWAY_NUMERIC_CONVERSION_REQUIRES_NUMERIC_TYPES",
            $"Gateway route '{route.Key}' requires numeric source and destination types for CheckedNumeric conversion.",
            route.Key));
        return false;
    }

    private static void AddMultipleWriterIssues(
        IReadOnlyCollection<ResolvedGatewayRoute> routes,
        List<RuntimeActivationIssue> issues)
    {
        foreach (var group in routes.GroupBy(route => route.Destination.Id).Where(group => group.Count() > 1))
        {
            foreach (var route in group)
            {
                issues.Add(new RuntimeActivationIssue(
                    "RUNTIME_GATEWAY_DESTINATION_MULTI_WRITER",
                    $"Gateway route '{route.Route.Key}' conflicts with another active route writing TAG '{route.Destination.Path}'.",
                    route.Route.Key));
            }
        }
    }

    private static void AddCycleIssues(
        IReadOnlyCollection<ResolvedGatewayRoute> routes,
        List<RuntimeActivationIssue> issues)
    {
        var adjacency = routes
            .GroupBy(route => route.Source.Id)
            .ToDictionary(group => group.Key, group => group.Select(route => route.Destination.Id).Distinct().ToArray());

        foreach (var route in routes)
        {
            if (!CanReach(route.Destination.Id, route.Source.Id, adjacency, new HashSet<Guid>())) continue;
            issues.Add(new RuntimeActivationIssue(
                "RUNTIME_GATEWAY_CYCLE_DETECTED",
                $"Gateway route '{route.Route.Key}' participates in an active TAG routing cycle.",
                route.Route.Key));
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

    private static object? ConvertValue(
        GatewayRouteEngineeringDto route,
        TagDataType sourceType,
        TagDataType destinationType,
        object? sourceValue)
    {
        if (sourceValue is null)
            throw new InvalidOperationException($"Gateway route '{route.Key}' cannot transfer a null source value.");

        if (route.ConversionPolicy == GatewayConversionPolicy.Exact)
            return sourceValue;

        if (!GatewayEngineeringValidator.IsNumeric(sourceType) || !GatewayEngineeringValidator.IsNumeric(destinationType))
            throw new InvalidOperationException($"Gateway route '{route.Key}' requires numeric types for CheckedNumeric conversion.");

        if (destinationType is TagDataType.Float or TagDataType.Double)
        {
            var numeric = Convert.ToDouble(sourceValue, System.Globalization.CultureInfo.InvariantCulture);
            var transformed = numeric * (route.Gain ?? 1d) + (route.Offset ?? 0d);
            if (double.IsNaN(transformed) || double.IsInfinity(transformed))
                throw new OverflowException("Gateway numeric transform produced a non-finite result.");

            if (destinationType == TagDataType.Float)
            {
                if (transformed > float.MaxValue || transformed < -float.MaxValue)
                    throw new OverflowException("Gateway numeric result is outside Float range.");
                return checked((float)transformed);
            }

            return transformed;
        }

        var decimalValue = Convert.ToDecimal(sourceValue, System.Globalization.CultureInfo.InvariantCulture);
        var gain = Convert.ToDecimal(route.Gain ?? 1d, System.Globalization.CultureInfo.InvariantCulture);
        var offset = Convert.ToDecimal(route.Offset ?? 0d, System.Globalization.CultureInfo.InvariantCulture);
        var result = checked(decimalValue * gain + offset);
        if (decimal.Truncate(result) != result)
            throw new OverflowException("Gateway checked integer conversion would discard a fractional value.");

        return destinationType switch
        {
            TagDataType.Int16 => checked((short)result),
            TagDataType.Int32 => checked((int)result),
            TagDataType.Int64 => checked((long)result),
            _ => throw new InvalidOperationException($"Unsupported Gateway numeric destination type '{destinationType}'.")
        };
    }

    private sealed record ResolvedGatewayRoute(
        Guid Id,
        GatewayRouteEngineeringDto Route,
        TagDefinition Source,
        TagDefinition Destination);

    private sealed class RouteRuntime
    {
        private readonly GatewayRuntimeEngine _owner;
        private readonly object _gate = new();
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private TagValue? _pending;
        private Task? _workerTask;
        private object? _lastObservedGoodSourceValue;
        private TagQuality? _lastObservedQuality;
        private object? _lastSuccessfulSourceValue;
        private DateTimeOffset? _lastWriteAttemptAtUtc;
        private GatewayRouteRuntimeState _state = GatewayRouteRuntimeState.Stopped;
        private DateTimeOffset? _lastSourceUpdateAtUtc;
        private DateTimeOffset? _lastSuccessfulTransferAtUtc;
        private DateTimeOffset? _lastFailedTransferAtUtc;
        private long _transferCount;
        private long _skippedTransferCount;
        private long _coalescedUpdateCount;
        private long _writeFailureCount;
        private int _consecutiveFailures;
        private string? _lastError;

        public RouteRuntime(GatewayRuntimeEngine owner, ResolvedGatewayRoute route)
        {
            _owner = owner;
            Id = route.Id;
            Route = route.Route;
            Source = route.Source;
            Destination = route.Destination;
        }

        public Guid Id { get; }
        public GatewayRouteEngineeringDto Route { get; }
        public TagDefinition Source { get; }
        public TagDefinition Destination { get; }

        public Task? WorkerTask
        {
            get { lock (_gate) return _workerTask; }
        }

        public void MarkStarted()
        {
            lock (_gate)
            {
                if (_state == GatewayRouteRuntimeState.Stopped)
                    _state = GatewayRouteRuntimeState.WaitingForSource;
            }
        }

        public void MarkStopped()
        {
            lock (_gate)
            {
                _state = GatewayRouteRuntimeState.Stopped;
                _pending = null;
                _lastObservedGoodSourceValue = null;
                _lastObservedQuality = null;
            }
        }

        public void QueueLatest(TagValue current, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _lastSourceUpdateAtUtc = current.Timestamp;
                if (current.Quality != TagQuality.Good)
                {
                    _lastObservedQuality = current.Quality;
                    _pending = null;
                    _skippedTransferCount++;
                    _state = GatewayRouteRuntimeState.WaitingForSource;
                    return;
                }

                var unchangedWhileGood =
                    _lastObservedQuality == TagQuality.Good &&
                    Equals(_lastObservedGoodSourceValue, current.Value);
                _lastObservedQuality = TagQuality.Good;
                _lastObservedGoodSourceValue = current.Value;
                if (unchangedWhileGood)
                {
                    _skippedTransferCount++;
                    return;
                }

                if (_pending is not null)
                    _coalescedUpdateCount++;
                _pending = current;

                if (_workerTask is null || _workerTask.IsCompleted)
                    _workerTask = Task.Run(() => ProcessPendingAsync(cancellationToken), CancellationToken.None);
            }
        }

        public void RecordNoSourceValue()
        {
            lock (_gate)
            {
                _skippedTransferCount++;
                _state = GatewayRouteRuntimeState.WaitingForSource;
            }
        }

        private async Task ProcessPendingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TagValue? signaled;
                    lock (_gate)
                    {
                        signaled = _pending;
                        _pending = null;
                    }
                    if (signaled is null) return;

                    if (Route.MinimumIntervalMilliseconds is int minimumInterval && _lastWriteAttemptAtUtc is DateTimeOffset lastAttempt)
                    {
                        var remaining = TimeSpan.FromMilliseconds(minimumInterval) - (DateTimeOffset.UtcNow - lastAttempt);
                        if (remaining > TimeSpan.Zero)
                            await Task.Delay(remaining, cancellationToken);
                    }

                    lock (_gate)
                    {
                        if (_pending is not null)
                        {
                            signaled = _pending;
                            _pending = null;
                        }
                    }

                    await TransferLatestAsync(signaled, applyDeadband: true, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        public async Task TransferLatestAsync(
            TagValue signaled,
            bool applyDeadband,
            CancellationToken cancellationToken)
        {
            _ = signaled; // The current cache is authoritative; signaled only schedules/coalesces work.
            await _writeGate.WaitAsync(cancellationToken);
            try
            {
                if (!_owner._cache.TryGet(Source.Id, out var latest) || latest is null)
                {
                    RecordNoSourceValue();
                    return;
                }

                lock (_gate) _lastSourceUpdateAtUtc = latest.Timestamp;
                if (latest.Quality != TagQuality.Good)
                {
                    lock (_gate)
                    {
                        _skippedTransferCount++;
                        _state = GatewayRouteRuntimeState.WaitingForSource;
                    }
                    return;
                }

                if (applyDeadband && Route.Deadband is double deadband && _lastSuccessfulSourceValue is not null)
                {
                    var currentNumeric = Convert.ToDouble(latest.Value, System.Globalization.CultureInfo.InvariantCulture);
                    var previousNumeric = Convert.ToDouble(_lastSuccessfulSourceValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (Math.Abs(currentNumeric - previousNumeric) < deadband)
                    {
                        lock (_gate) _skippedTransferCount++;
                        return;
                    }
                }

                lock (_gate) _lastWriteAttemptAtUtc = DateTimeOffset.UtcNow;
                try
                {
                    await _owner.WriteDestinationAsync(this, latest, cancellationToken);
                    lock (_gate)
                    {
                        _lastSuccessfulSourceValue = latest.Value;
                        _lastSuccessfulTransferAtUtc = DateTimeOffset.UtcNow;
                        _transferCount++;
                        _consecutiveFailures = 0;
                        _lastError = null;
                        _state = GatewayRouteRuntimeState.Running;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (_gate)
                    {
                        _lastFailedTransferAtUtc = DateTimeOffset.UtcNow;
                        _writeFailureCount++;
                        _consecutiveFailures++;
                        _lastError = SanitizeError(ex);
                        _state = GatewayRouteRuntimeState.Degraded;
                    }
                }
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public GatewayRouteRuntimeDiagnostic Snapshot()
        {
            lock (_gate)
            {
                return new GatewayRouteRuntimeDiagnostic(
                    Id,
                    Route.Key,
                    Route.Name,
                    Route.Enabled,
                    _state,
                    Source.Id,
                    Source.Path,
                    Source.Source,
                    Destination.Id,
                    Destination.Path,
                    Destination.Source,
                    _lastSourceUpdateAtUtc,
                    _lastSuccessfulTransferAtUtc,
                    _lastFailedTransferAtUtc,
                    _transferCount,
                    _skippedTransferCount,
                    _coalescedUpdateCount,
                    _writeFailureCount,
                    _consecutiveFailures,
                    _lastError,
                    _pending is not null,
                    Route.TransferMode,
                    Route.TransferMode == GatewayTransferMode.Periodic
                        ? Route.PeriodMilliseconds
                        : Route.MinimumIntervalMilliseconds);
            }
        }

        private static string SanitizeError(Exception ex)
        {
            var message = ex.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (message.Length > 240) message = message[..240];
            return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : $"{ex.GetType().Name}: {message}";
        }
    }
}
