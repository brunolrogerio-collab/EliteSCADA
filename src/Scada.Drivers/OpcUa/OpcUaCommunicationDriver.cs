using System.Diagnostics;
using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

public sealed class OpcUaCommunicationDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private const int RecentOutcomeWindow = 100;

    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<OpcUaRuntimeBinding> _bindings;
    private readonly IReadOnlyDictionary<Guid, OpcUaRuntimeBinding> _bindingsByTagId;
    private readonly OpcUaRuntimeSessionSupervisor _sessions;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private readonly string? _endpoint;
    private readonly TimeSpan? _publishingInterval;

    private CancellationTokenSource? _runtimeCts;
    private Task? _subscriptionLoop;
    private long _updatesPublished;
    private int _disposed;

    private CommunicationDriverOperationalState _communicationState;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;
    private long _subscriptionCycles;
    private long _requestAttempts;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private long _writeOperations;
    private long _timedOperationCount;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;

    public OpcUaCommunicationDriver(
        string driverId,
        string name,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<TagDefinition> tags,
        IOpcUaRuntimeSessionFactory sessionFactory,
        IReadOnlyList<TimeSpan>? reconnectDelays = null,
        string? endpoint = null,
        TimeSpan? publishingInterval = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        if (publishingInterval.HasValue && publishingInterval.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(publishingInterval));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _bindings = tags.Select(OpcUaRuntimeBinding.FromTag).ToArray();
        if (_bindings.Count == 0) throw new ArgumentException("At least one OPC UA TAG is required.", nameof(tags));
        if (_bindings.Select(x => x.Tag.Id).Distinct().Count() != _bindings.Count)
            throw new ArgumentException("Each OPC UA binding must reference a unique TAG ID.", nameof(tags));

        _bindingsByTagId = _bindings.ToDictionary(x => x.Tag.Id);
        _sessions = new OpcUaRuntimeSessionSupervisor(sessionFactory, reconnectDelays);
        _endpoint = SanitizeEndpoint(endpoint);
        _publishingInterval = publishingInterval;
        var now = DateTimeOffset.UtcNow;
        _communicationState = CommunicationDriverOperationalState.Stopped;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, now);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read |
        DriverCapabilities.Write |
        DriverCapabilities.Subscribe |
        DriverCapabilities.Diagnostics |
        DriverCapabilities.SourceTimestamp |
        DriverCapabilities.ServerTimestamp;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _bindings.Select(x => x.Tag).ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_runtimeCts is { IsCancellationRequested: false }) return;
            Status = NewStatus(DriverState.Starting);
            TransitionCommunicationState(CommunicationDriverOperationalState.Starting);
            RegisterTags();

            try
            {
                await _sessions.ConnectAsync(_bindings, cancellationToken).ConfigureAwait(false);
                RecordConnected(reconnect: false);
                _runtimeCts = new CancellationTokenSource();
                Status = NewStatus(DriverState.Running);
                TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
                _subscriptionLoop = PumpSubscriptionsAsync(_runtimeCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
                Status = NewStatus(DriverState.Stopped);
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
                Status = NewStatus(DriverState.Faulted, SanitizeError(ex));
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cts = _runtimeCts;
            if (cts is null)
            {
                var hadSession = _sessions.CurrentSession is not null;
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                if (hadSession) RecordDisconnected();
                Status = NewStatus(DriverState.Stopped);
                TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
                return;
            }

            Status = NewStatus(DriverState.Stopping);
            TransitionCommunicationState(CommunicationDriverOperationalState.Stopping);
            await cts.CancelAsync().ConfigureAwait(false);
            if (_subscriptionLoop is not null)
            {
                try
                {
                    await _subscriptionLoop.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
            }

            var hadActiveSession = _sessions.CurrentSession is not null;
            await _sessions.DisconnectAsync().ConfigureAwait(false);
            if (hadActiveSession) RecordDisconnected();
            cts.Dispose();
            _runtimeCts = null;
            _subscriptionLoop = null;
            Status = NewStatus(DriverState.Stopped);
            TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        GetBinding(tagId);
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var binding = GetBinding(tagId);
        OpcUaRuntimeValueValidator.ValidateWrite(binding.Tag, value);
        var session = _sessions.CurrentSession
            ?? throw new InvalidOperationException("OPC UA runtime session is not available.");

        var started = Stopwatch.GetTimestamp();
        try
        {
            await session.WriteAsync(binding, value!, cancellationToken).ConfigureAwait(false);
            RecordWrite(success: true, Stopwatch.GetElapsedTime(started), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordWrite(success: false, Stopwatch.GetElapsedTime(started), ex);
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            throw;
        }

        await PublishAsync(new OpcUaRuntimeDataValue(tagId, value, TagQuality.Good), cancellationToken)
            .ConfigureAwait(false);
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var quality = BuildQualitySummary();

        lock (_diagnosticsGate)
        {
            var averageDuration = _timedOperationCount == 0
                ? (TimeSpan?)null
                : TimeSpan.FromTicks(_totalOperationDurationTicks / _timedOperationCount);
            var failureRate = _recentFailures.Count == 0
                ? 0d
                : _recentFailures.Count(x => x) / (double)_recentFailures.Count;
            var dataAge = _lastSuccessfulCommunicationAt.HasValue
                ? capturedAt - _lastSuccessfulCommunicationAt.Value
                : (TimeSpan?)null;
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["acquisitionMode"] = "Subscription",
                ["bindingCount"] = _bindings.Count.ToString(CultureInfo.InvariantCulture),
                ["writableBindingCount"] = _bindings.Count(x => x.Writable).ToString(CultureInfo.InvariantCulture),
                ["namespaceUriBindingCount"] = _bindings.Count(x => x.Node.NamespaceUri is not null).ToString(CultureInfo.InvariantCulture),
                ["requestCounterScope"] = "driverInitiatedWritesOnly"
            };
            if (_publishingInterval.HasValue)
            {
                details["publishingIntervalMs"] = _publishingInterval.Value.TotalMilliseconds
                    .ToString("0.###", CultureInfo.InvariantCulture);
            }

            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                OpcUaDriverDescriptorProvider.DriverTypeId,
                _runtimeInstanceId,
                _endpoint,
                _communicationState,
                _stateChangedAt,
                capturedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                dataAge,
                _publishingInterval,
                _timedOperationCount == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                averageDuration,
                null,
                failureRate,
                _bindings.Count,
                quality,
                new CommunicationDriverCounters(
                    _subscriptionCycles,
                    _requestAttempts,
                    _successfulOperations,
                    _failedOperations,
                    _consecutiveFailures,
                    _timeouts,
                    _connections,
                    _disconnections,
                    _reconnects,
                    0,
                    _writeOperations,
                    Interlocked.Read(ref _updatesPublished)),
                details);
        }
    }

    private async Task PumpSubscriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var session = _sessions.CurrentSession;
                if (session is null)
                {
                    TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
                    session = await _sessions.ReconnectUntilAvailableAsync(
                        _bindings,
                        attempt =>
                        {
                            RecordFailureMessage($"OPC UA reconnect attempt {attempt} failed.");
                            Status = NewStatus(
                                DriverState.Faulted,
                                $"OPC UA reconnect attempt {attempt} failed.");
                        },
                        cancellationToken).ConfigureAwait(false);
                    RecordConnected(reconnect: true);
                    Status = NewStatus(DriverState.Running, "OPC UA runtime session reconnected.");
                    TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
                }

                try
                {
                    RecordSubscriptionCycle();
                    await foreach (var observed in session.SubscribeAsync(cancellationToken).WithCancellation(cancellationToken))
                    {
                        if (_bindingsByTagId.ContainsKey(observed.TagId))
                        {
                            RecordSubscriptionUpdate();
                            await PublishAsync(observed, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                        throw new InvalidOperationException("OPC UA subscription ended unexpectedly.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RecordFailure(ex);
                    Status = NewStatus(DriverState.Faulted, "OPC UA subscription interrupted. Reconnecting.");
                    TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
                    await PublishCommunicationFailureAsync(cancellationToken).ConfigureAwait(false);
                    await _sessions.InvalidateAsync(session).ConfigureAwait(false);
                    RecordDisconnected();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
            Status = NewStatus(DriverState.Faulted, SanitizeError(ex));
        }
    }

    private async Task PublishCommunicationFailureAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in _bindings)
        {
            _cache.TryGet(binding.Tag.Id, out var previous);
            var failed = new TagValue(
                binding.Tag.Id,
                previous?.Value,
                DateTimeOffset.UtcNow,
                TagQuality.BadCommunication,
                DriverId)
            {
                SourceTimestamp = previous?.SourceTimestamp,
                ServerTimestamp = previous?.ServerTimestamp
            };
            await UpdateCacheAsync(binding.Tag, failed, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(OpcUaRuntimeDataValue observed, CancellationToken cancellationToken)
    {
        var binding = GetBinding(observed.TagId);
        var value = new TagValue(
            observed.TagId,
            observed.Value,
            DateTimeOffset.UtcNow,
            observed.Quality,
            DriverId)
        {
            SourceTimestamp = observed.SourceTimestamp,
            ServerTimestamp = observed.ServerTimestamp
        };
        return UpdateCacheAsync(binding.Tag, value, cancellationToken);
    }

    private async Task UpdateCacheAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken)
    {
        await _cache.UpdateAsync(tag, value, cancellationToken).ConfigureAwait(false);
        var count = Interlocked.Increment(ref _updatesPublished);
        Status = Status with { Timestamp = DateTimeOffset.UtcNow, UpdatesPublished = count };
    }

    private OpcUaRuntimeBinding GetBinding(Guid tagId) =>
        _bindingsByTagId.TryGetValue(tagId, out var binding)
            ? binding
            : throw new KeyNotFoundException($"OPC UA TAG '{tagId}' was not found in driver '{DriverId}'.");

    private void RegisterTags()
    {
        foreach (var binding in _bindings)
        {
            if (_registry.TryGet(binding.Tag.Id, out var existing) && existing is not null)
            {
                if (!existing.Path.Equals(binding.Tag.Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"OPC UA TAG '{binding.Tag.Id}' is already registered with path '{existing.Path}', expected '{binding.Tag.Path}'.");
                continue;
            }
            _registry.Register(binding.Tag);
        }
    }

    private void RecordConnected(bool reconnect)
    {
        lock (_diagnosticsGate)
        {
            _connections++;
            if (reconnect) _reconnects++;
            RecordOutcomeUnsafe(success: true, null);
        }
    }

    private void RecordDisconnected()
    {
        lock (_diagnosticsGate)
        {
            _disconnections++;
        }
    }

    private void RecordSubscriptionCycle()
    {
        lock (_diagnosticsGate)
        {
            _subscriptionCycles++;
        }
    }

    private void RecordSubscriptionUpdate()
    {
        lock (_diagnosticsGate)
        {
            RecordOutcomeUnsafe(success: true, null);
        }
    }

    private void RecordWrite(bool success, TimeSpan duration, Exception? error)
    {
        lock (_diagnosticsGate)
        {
            _requestAttempts++;
            _writeOperations++;
            _timedOperationCount++;
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            RecordOutcomeUnsafe(success, error);
        }
    }

    private void RecordFailure(Exception error)
    {
        lock (_diagnosticsGate)
        {
            RecordOutcomeUnsafe(success: false, error);
        }
    }

    private void RecordFailureMessage(string message)
    {
        lock (_diagnosticsGate)
        {
            RecordOutcomeUnsafe(success: false, null, message);
        }
    }

    private void RecordOutcomeUnsafe(bool success, Exception? error, string? explicitMessage = null)
    {
        var now = DateTimeOffset.UtcNow;
        _recentFailures.Enqueue(!success);
        while (_recentFailures.Count > RecentOutcomeWindow) _recentFailures.Dequeue();

        if (success)
        {
            _successfulOperations++;
            _consecutiveFailures = 0;
            _lastSuccessfulCommunicationAt = now;
            return;
        }

        _failedOperations++;
        _consecutiveFailures++;
        _lastFailedCommunicationAt = now;
        if (error is TimeoutException) _timeouts++;
        _lastError = explicitMessage ?? SanitizeError(error);
    }

    private void TransitionCommunicationState(CommunicationDriverOperationalState state)
    {
        lock (_diagnosticsGate)
        {
            if (_communicationState == state) return;
            _communicationState = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0;
        var badCommunication = 0;
        var uncertain = 0;
        var bad = 0;
        var badConfiguration = 0;
        var badDevice = 0;
        var stale = 0;
        var disabled = 0;
        var noSample = 0;

        foreach (var binding in _bindings)
        {
            if (!_cache.TryGet(binding.Tag.Id, out var sample) || sample is null)
            {
                noSample++;
                continue;
            }

            switch (sample.Quality)
            {
                case TagQuality.Good: good++; break;
                case TagQuality.BadCommunication: badCommunication++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.Bad: bad++; break;
                case TagQuality.BadConfiguration: badConfiguration++; break;
                case TagQuality.BadDevice: badDevice++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
                default: bad++; break;
            }
        }

        return new CommunicationTagQualitySummary(
            good,
            badCommunication,
            uncertain,
            bad,
            badConfiguration,
            badDevice,
            stale,
            disabled,
            noSample);
    }

    private DriverStatus NewStatus(DriverState state, string? message = null) =>
        new(DriverId, Name, state, DateTimeOffset.UtcNow, message, _updatesPublished);

    private static string? SanitizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var trimmed = endpoint.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static string SanitizeError(Exception? error)
    {
        if (error is null) return string.Empty;
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(OpcUaCommunicationDriver));
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _sessions.DisposeAsync().ConfigureAwait(false);
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _lifecycleGate.Dispose();
    }
}
