using System.Diagnostics;
using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mqtt;

public delegate ValueTask<MqttResolvedCredentials> MqttCredentialResolver(CancellationToken cancellationToken);

public sealed class MqttDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private const int RecentOutcomeWindow = 100;

    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<MqttPoint> _points;
    private readonly IReadOnlyList<MqttPoint> _freshnessPoints;
    private readonly IReadOnlyDictionary<Guid, MqttPoint> _pointsByTagId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<MqttPoint>> _pointsByTopic;
    private readonly Dictionary<Guid, long> _freshnessReferenceByTagId = new();
    private readonly MqttConnectionSettings _settings;
    private readonly IMqttClientTransport _transport;
    private readonly MqttCredentialResolver _credentialResolver;
    private readonly TimeSpan? _freshnessCheckInterval;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SemaphoreSlim _freshnessGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Task? _freshnessLoop;
    private CommunicationDriverOperationalState _communicationState;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private DateTimeOffset? _lastAcceptedMessageAt;
    private string? _lastError;
    private long _requests;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private long _readOperations;
    private long _writeOperations;
    private long _updatesPublished;
    private long _messagesReceived;
    private long _retainedMessages;
    private long _decodeFailures;
    private long _unexpectedMessages;
    private long _connectAttempts;
    private long _subscribeRequests;
    private long _freshnessTransitions;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private long _timedOperations;
    private int _consecutiveConnectFailures;
    private bool _hasConnectedOnce;

    public MqttDriver(
        string driverId,
        string name,
        MqttConnectionSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<MqttPoint> points,
        IMqttClientTransport transport,
        MqttCredentialResolver? credentialResolver = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(transport);

        settings.Validate();
        var pointArray = points.ToArray();
        if (pointArray.Length == 0) throw new ArgumentException("At least one MQTT point is required.", nameof(points));
        foreach (var point in pointArray) point.Validate();
        if (pointArray.Select(point => point.Tag.Id).Distinct().Count() != pointArray.Length)
            throw new ArgumentException("Each MQTT point must reference a unique TAG ID.", nameof(points));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _settings = settings;
        _cache = cache;
        _registry = registry;
        _points = pointArray;
        _freshnessPoints = pointArray.Where(point => point.FreshnessTimeout.HasValue).ToArray();
        _freshnessCheckInterval = ComputeFreshnessCheckInterval(_freshnessPoints);
        _pointsByTagId = pointArray.ToDictionary(point => point.Tag.Id);
        _pointsByTopic = pointArray
            .GroupBy(point => point.SubscribeTopic, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MqttPoint>)group.ToArray(),
                StringComparer.Ordinal);
        _transport = transport;
        _credentialResolver = credentialResolver ?? (_ => ValueTask.FromResult(MqttResolvedCredentials.None));

        var now = DateTimeOffset.UtcNow;
        _communicationState = CommunicationDriverOperationalState.Stopped;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, now);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Tag).ToArray();

    public DriverCapabilities Capabilities
    {
        get
        {
            var capabilities = DriverCapabilities.Read | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;
            if (_points.Any(point => point.Writable)) capabilities |= DriverCapabilities.Write;
            if (_points.Any(point => point.SourceTimestampJsonPointer is not null)) capabilities |= DriverCapabilities.SourceTimestamp;
            return capabilities;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        foreach (var point in _points) _registry.Register(point.Tag);
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionCommunicationState(CommunicationDriverOperationalState.Starting);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        _freshnessLoop = _freshnessCheckInterval.HasValue
            ? RunFreshnessAsync(_freshnessCheckInterval.Value, _cts.Token)
            : null;
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;

        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        TransitionCommunicationState(CommunicationDriverOperationalState.Stopping);
        await _cts.CancelAsync();

        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }

        if (_freshnessLoop is not null)
        {
            try { await _freshnessLoop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }

        await DisconnectTransportAsync(cancellationToken);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"MQTT TAG '{tagId}' was not found in driver '{DriverId}'.");

        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"MQTT TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable)
            throw new InvalidOperationException($"MQTT TAG '{point.Tag.Path}' is not writable.");
        if (!_transport.IsConnected)
            throw new MqttTransportException("MQTT broker is not connected.");

        var payload = MqttPayloadCodec.Encode(point, value);
        var request = new MqttPublishRequest(point.PublishTopic!, payload, point.PublishQos, point.PublishRetain);

        await _writeGate.WaitAsync(cancellationToken);
        var started = Stopwatch.GetTimestamp();
        try
        {
            Interlocked.Increment(ref _requests);
            await _transport.PublishAsync(request, cancellationToken);
            Interlocked.Increment(ref _writeOperations);
            RecordOperation(success: true, Stopwatch.GetElapsedTime(started), null);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            if (ex is TimeoutException) Interlocked.Increment(ref _timeouts);
            Interlocked.Increment(ref _writeOperations);
            RecordOperation(success: false, Stopwatch.GetElapsedTime(started), ex);
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            var averageDuration = _timedOperations == 0
                ? (TimeSpan?)null
                : TimeSpan.FromTicks(_totalOperationDurationTicks / _timedOperations);
            var failureRate = _recentFailures.Count == 0
                ? 0d
                : _recentFailures.Count(failed => failed) / (double)_recentFailures.Count;
            var dataAge = _lastAcceptedMessageAt.HasValue ? capturedAt - _lastAcceptedMessageAt.Value : (TimeSpan?)null;

            var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["protocol"] = _settings.ProtocolMode == MqttProtocolMode.Mqtt5 ? "5.0" : "3.1.1",
                ["tls"] = _settings.UseTls ? "true" : "false",
                ["clientId"] = _settings.ClientId,
                ["subscriptionCount"] = _pointsByTopic.Count.ToString(CultureInfo.InvariantCulture),
                ["maximumBufferedMessages"] = _settings.MaximumBufferedMessages.ToString(CultureInfo.InvariantCulture),
                ["freshnessPointCount"] = _freshnessPoints.Count.ToString(CultureInfo.InvariantCulture),
                ["freshnessTransitions"] = _freshnessTransitions.ToString(CultureInfo.InvariantCulture),
                ["messagesReceived"] = _messagesReceived.ToString(CultureInfo.InvariantCulture),
                ["retainedMessages"] = _retainedMessages.ToString(CultureInfo.InvariantCulture),
                ["decodeFailures"] = _decodeFailures.ToString(CultureInfo.InvariantCulture),
                ["unexpectedMessages"] = _unexpectedMessages.ToString(CultureInfo.InvariantCulture),
                ["connectAttempts"] = _connectAttempts.ToString(CultureInfo.InvariantCulture),
                ["subscribeRequests"] = _subscribeRequests.ToString(CultureInfo.InvariantCulture)
            };

            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                "mqtt.raw",
                _runtimeInstanceId,
                $"{_settings.Host}:{_settings.Port}",
                _communicationState,
                _stateChangedAt,
                capturedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                dataAge,
                null,
                _timedOperations == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                averageDuration,
                null,
                failureRate,
                _points.Count,
                BuildQualitySummary(),
                new CommunicationDriverCounters(
                    Cycles: 0,
                    Requests: Interlocked.Read(ref _requests),
                    SuccessfulOperations: _successfulOperations,
                    FailedOperations: _failedOperations,
                    ConsecutiveFailures: _consecutiveFailures,
                    Timeouts: Interlocked.Read(ref _timeouts),
                    Connections: _connections,
                    Disconnections: _disconnections,
                    Reconnects: _reconnects,
                    ReadOperations: Interlocked.Read(ref _readOperations),
                    WriteOperations: Interlocked.Read(ref _writeOperations),
                    UpdatesPublished: Interlocked.Read(ref _updatesPublished)),
                protocolDetails);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(); }
        finally
        {
            _cts?.Dispose();
            _writeGate.Dispose();
            _freshnessGate.Dispose();
            await _transport.DisposeAsync();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var reconnectDelay = _settings.EffectiveReconnectMinimumDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_transport.IsConnected)
                {
                    await ConnectAndSubscribeAsync(cancellationToken);
                    reconnectDelay = _settings.EffectiveReconnectMinimumDelay;
                }

                var message = await _transport.ReceiveAsync(cancellationToken);
                await HandleMessageAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                if (ex is TimeoutException) Interlocked.Increment(ref _timeouts);
                RecordFailureOnly(ex);
                _consecutiveConnectFailures++;
                await MarkAllCommunicationFailureAsync(cancellationToken);
                await DisconnectTransportAsync(CancellationToken.None);

                var permanent = ex is MqttTransportException mqtt && mqtt.IsPermanent;
                if (permanent || _consecutiveConnectFailures >= _settings.MaximumConsecutiveConnectFailures)
                {
                    TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
                    Status = new DriverStatus(
                        DriverId,
                        Name,
                        DriverState.Faulted,
                        DateTimeOffset.UtcNow,
                        SanitizeError(ex),
                        Interlocked.Read(ref _updatesPublished));
                    return;
                }

                TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Running,
                    DateTimeOffset.UtcNow,
                    SanitizeError(ex),
                    Interlocked.Read(ref _updatesPublished));

                await Task.Delay(reconnectDelay, cancellationToken);
                reconnectDelay = NextReconnectDelay(reconnectDelay);
            }
            catch (Exception ex)
            {
                RecordFailureOnly(ex);
                TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Faulted,
                    DateTimeOffset.UtcNow,
                    SanitizeError(ex),
                    Interlocked.Read(ref _updatesPublished));
                return;
            }
        }
    }

    private async Task RunFreshnessAsync(TimeSpan checkInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, cancellationToken);
            var nowTimestamp = Stopwatch.GetTimestamp();
            var nowUtc = DateTimeOffset.UtcNow;

            foreach (var point in _freshnessPoints)
            {
                await _freshnessGate.WaitAsync(cancellationToken);
                try
                {
                    long referenceTimestamp;
                    lock (_diagnosticsGate)
                    {
                        if (!_freshnessReferenceByTagId.TryGetValue(point.Tag.Id, out referenceTimestamp))
                            continue;
                    }

                    var timeout = point.FreshnessTimeout!.Value;
                    if (Stopwatch.GetElapsedTime(referenceTimestamp, nowTimestamp) <= timeout)
                        continue;
                    if (!_cache.TryGet(point.Tag.Id, out var current) || current is null || current.Quality != TagQuality.Good)
                        continue;

                    var stale = current with
                    {
                        Timestamp = nowUtc,
                        Quality = TagQuality.Stale
                    };
                    await _cache.UpdateAsync(point.Tag, stale, cancellationToken);
                    Interlocked.Increment(ref _updatesPublished);
                    lock (_diagnosticsGate) _freshnessTransitions++;
                }
                finally
                {
                    _freshnessGate.Release();
                }
            }
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        using var credentials = await _credentialResolver(cancellationToken);
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _connectAttempts);
        Interlocked.Increment(ref _requests);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_settings.EffectiveConnectTimeout);
            await _transport.ConnectAsync(_settings, credentials, timeout.Token);
            lock (_diagnosticsGate)
            {
                _connections++;
                if (_hasConnectedOnce) _reconnects++;
                _hasConnectedOnce = true;
            }
            RecordOperation(success: true, Stopwatch.GetElapsedTime(started), null);

            var subscriptions = _pointsByTopic
                .Select(pair => new MqttSubscription(
                    pair.Key,
                    (MqttQosLevel)pair.Value.Max(point => (int)point.Qos)))
                .ToArray();

            if (subscriptions.Length > 0)
            {
                var subscribeStarted = Stopwatch.GetTimestamp();
                Interlocked.Increment(ref _subscribeRequests);
                Interlocked.Increment(ref _requests);
                await _transport.SubscribeAsync(subscriptions, cancellationToken);
                RecordOperation(success: true, Stopwatch.GetElapsedTime(subscribeStarted), null);
            }

            _consecutiveConnectFailures = 0;
            TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Running,
                DateTimeOffset.UtcNow,
                UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _timeouts);
            var error = new TimeoutException($"MQTT connect timed out after {_settings.EffectiveConnectTimeout}.");
            RecordOperation(success: false, Stopwatch.GetElapsedTime(started), error);
            throw error;
        }
        catch (Exception ex)
        {
            RecordOperation(success: false, Stopwatch.GetElapsedTime(started), ex);
            throw;
        }
    }

    private async Task HandleMessageAsync(MqttTransportMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _messagesReceived);
        if (message.Retained) Interlocked.Increment(ref _retainedMessages);

        if (!_pointsByTopic.TryGetValue(message.Topic, out var points))
        {
            Interlocked.Increment(ref _unexpectedMessages);
            return;
        }

        if (message.Payload.Length > _settings.MaximumInboundPayloadBytes)
        {
            var error = new MqttPayloadException(
                $"MQTT payload on topic '{message.Topic}' exceeds the configured maximum of {_settings.MaximumInboundPayloadBytes} bytes.");
            foreach (var point in points)
            {
                Interlocked.Increment(ref _readOperations);
                Interlocked.Increment(ref _decodeFailures);
                RecordUntimedOutcome(success: false, error);
                await PublishFailureAsync(point, TagQuality.Bad, cancellationToken);
            }
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            SetRunningStatus(error.Message);
            return;
        }

        var failed = 0;
        string? lastError = null;
        foreach (var point in points)
        {
            Interlocked.Increment(ref _readOperations);
            try
            {
                var decoded = MqttPayloadCodec.Decode(
                    point,
                    message.Payload.Span,
                    message.Retained,
                    message.ReceivedAtUtc);

                var sample = new TagValue(
                    point.Tag.Id,
                    decoded.Value,
                    message.ReceivedAtUtc,
                    decoded.Quality,
                    DriverId)
                {
                    SourceTimestamp = decoded.SourceTimestamp
                };
                await PublishAcceptedSampleAsync(point, sample, message.ReceivedAtUtc, cancellationToken);
                RecordUntimedOutcome(success: true, null);
            }
            catch (MqttPayloadException ex)
            {
                failed++;
                lastError = SanitizeError(ex);
                Interlocked.Increment(ref _decodeFailures);
                RecordUntimedOutcome(success: false, ex);
                await PublishFailureAsync(point, TagQuality.Bad, cancellationToken);
            }
        }

        if (failed == 0)
        {
            TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
            SetRunningStatus(null);
        }
        else
        {
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            SetRunningStatus($"{failed} MQTT mapping(s) failed on topic '{message.Topic}'. Last error: {lastError}");
        }
    }

    private async Task PublishAcceptedSampleAsync(
        MqttPoint point,
        TagValue sample,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!point.FreshnessTimeout.HasValue)
        {
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
            Interlocked.Increment(ref _updatesPublished);
            lock (_diagnosticsGate) _lastAcceptedMessageAt = receivedAtUtc;
            return;
        }

        await _freshnessGate.WaitAsync(cancellationToken);
        try
        {
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
            Interlocked.Increment(ref _updatesPublished);
            var freshnessReference = Stopwatch.GetTimestamp();
            lock (_diagnosticsGate)
            {
                _lastAcceptedMessageAt = receivedAtUtc;
                _freshnessReferenceByTagId[point.Tag.Id] = freshnessReference;
            }
        }
        finally
        {
            _freshnessGate.Release();
        }
    }

    private async Task PublishFailureAsync(MqttPoint point, TagQuality quality, CancellationToken cancellationToken)
    {
        if (!point.FreshnessTimeout.HasValue)
        {
            await PublishFailureCoreAsync(point, quality, cancellationToken);
            return;
        }

        await _freshnessGate.WaitAsync(cancellationToken);
        try
        {
            await PublishFailureCoreAsync(point, quality, cancellationToken);
        }
        finally
        {
            _freshnessGate.Release();
        }
    }

    private async Task PublishFailureCoreAsync(MqttPoint point, TagQuality quality, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        var sample = new TagValue(
            point.Tag.Id,
            previous?.Value,
            DateTimeOffset.UtcNow,
            quality,
            DriverId)
        {
            SourceTimestamp = previous?.SourceTimestamp
        };
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private async Task MarkAllCommunicationFailureAsync(CancellationToken cancellationToken)
    {
        foreach (var point in _points)
            await PublishFailureAsync(point, TagQuality.BadCommunication, cancellationToken);
    }

    private async ValueTask DisconnectTransportAsync(CancellationToken cancellationToken)
    {
        if (!_transport.IsConnected) return;
        try
        {
            await _transport.DisconnectAsync(cancellationToken);
        }
        finally
        {
            lock (_diagnosticsGate) _disconnections++;
        }
    }

    private TimeSpan NextReconnectDelay(TimeSpan current)
    {
        var doubledTicks = current.Ticks > long.MaxValue / 2 ? long.MaxValue : current.Ticks * 2;
        return TimeSpan.FromTicks(Math.Min(doubledTicks, _settings.EffectiveReconnectMaximumDelay.Ticks));
    }

    private void SetRunningStatus(string? message)
    {
        Status = new DriverStatus(
            DriverId,
            Name,
            DriverState.Running,
            DateTimeOffset.UtcNow,
            message,
            Interlocked.Read(ref _updatesPublished));
    }

    private void RecordOperation(bool success, TimeSpan duration, Exception? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            _timedOperations++;
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            RecordOutcomeLocked(success, error, now);
        }
    }

    private void RecordUntimedOutcome(bool success, Exception? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate) RecordOutcomeLocked(success, error, now);
    }

    private void RecordOutcomeLocked(bool success, Exception? error, DateTimeOffset now)
    {
        _recentFailures.Enqueue(!success);
        while (_recentFailures.Count > RecentOutcomeWindow) _recentFailures.Dequeue();

        if (success)
        {
            _successfulOperations++;
            _consecutiveFailures = 0;
            _lastSuccessfulCommunicationAt = now;
        }
        else
        {
            _failedOperations++;
            _consecutiveFailures++;
            _lastFailedCommunicationAt = now;
            _lastError = SanitizeError(error);
        }
    }

    private void RecordFailureOnly(Exception error)
    {
        lock (_diagnosticsGate)
        {
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = SanitizeError(error);
        }
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

        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var sample) || sample is null)
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

    private static TimeSpan? ComputeFreshnessCheckInterval(IReadOnlyCollection<MqttPoint> points)
    {
        if (points.Count == 0) return null;

        var minimumTimeoutTicks = points.Min(point => point.FreshnessTimeout!.Value.Ticks);
        var proposedTicks = Math.Max(1, minimumTimeoutTicks / 4);
        var minimumCheckTicks = TimeSpan.FromMilliseconds(25).Ticks;
        var maximumCheckTicks = TimeSpan.FromSeconds(1).Ticks;
        return TimeSpan.FromTicks(Math.Clamp(proposedTicks, minimumCheckTicks, maximumCheckTicks));
    }

    private static string SanitizeError(Exception? error)
    {
        if (error is null) return string.Empty;
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }
}
