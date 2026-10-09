using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Zigbee2Mqtt;

public delegate ValueTask<MqttResolvedCredentials> Zigbee2MqttCredentialResolver(CancellationToken cancellationToken);

/// <summary>
/// Runtime adapter for one user-managed Zigbee2MQTT instance. The MQTT bridge is
/// only a transport and inventory source: broker PUBACK never updates a TAG.
/// TAG values are updated only from state reports, with retained/startup-cache
/// evidence kept stale or uncertain until a fresh read/report is established.
/// </summary>
public sealed class Zigbee2MqttDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly Zigbee2MqttConnectionSettings _settings;
    private readonly Guid _dataSourceId;
    private readonly IReadOnlyList<Zigbee2MqttPoint> _points;
    private readonly IReadOnlyDictionary<Guid, Zigbee2MqttPoint> _pointsByTag;
    private readonly Func<IMqttClientTransport> _transportFactory;
    private readonly Zigbee2MqttCredentialResolver _credentialResolver;
    private readonly Func<bool> _effectAuthority;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Dictionary<Guid, PendingOperation> _pending = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private IMqttClientTransport? _transport;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private Zigbee2MqttInventory? _inventory;
    private Dictionary<string, string> _friendlyNameByIeee = new(StringComparer.Ordinal);
    private Dictionary<string, string> _ieeeByStateTopic = new(StringComparer.Ordinal);
    private Dictionary<string, string> _ieeeByAvailabilityTopic = new(StringComparer.Ordinal);
    private HashSet<Guid> _activeTagIds = new();
    private volatile string? _bridgeState;
    private bool _hasConnectedOnce;
    private bool _disposed;
    private volatile bool _isReady;
    private DateTimeOffset _sessionStartedAt;
    private DateTimeOffset _freshnessFloorUtc;
    private int _consecutiveConnectFailures;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private DateTimeOffset? _lastValueAt;
    private string? _lastError;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private CommunicationDriverReadinessState _readinessState = CommunicationDriverReadinessState.Stopped;
    private long _requests;
    private long _successfulOperations;
    private long _failedOperations;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private long _reads;
    private long _writes;
    private long _updates;
    private long _messages;
    private long _retainedMessages;
    private long _rejectedValues;
    private long _ambiguousWrites;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private long _timedOperations;

    public Zigbee2MqttDriver(
        string driverId,
        string name,
        Guid dataSourceId,
        Zigbee2MqttConnectionSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<Zigbee2MqttPoint> points,
        Func<IMqttClientTransport> transportFactory,
        Zigbee2MqttCredentialResolver credentialResolver,
        Func<bool>? effectAuthority = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        if (dataSourceId == Guid.Empty) throw new ArgumentException("DataSourceId is required.", nameof(dataSourceId));
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToArray();
        foreach (var point in _points) point.Validate();
        if (_points.Select(point => point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Zigbee2MQTT runtime plan contains duplicate TAG IDs.", nameof(points));
        if (_points.Select(point => Zigbee2MqttIdentity.PortableAddress(point.IeeeAddress, point.Endpoint, point.Property)).Distinct(StringComparer.Ordinal).Count() != _points.Count)
            throw new ArgumentException("Zigbee2MQTT runtime plan contains ambiguous IEEE/endpoint/property bindings.", nameof(points));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _dataSourceId = dataSourceId;
        _settings = settings;
        _pointsByTag = _points.ToDictionary(point => point.Tag.Id);
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _effectAuthority = effectAuthority ?? (() => true);
        _stateChangedAt = DateTimeOffset.UtcNow;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, _stateChangedAt);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Subscribe |
                                              DriverCapabilities.Diagnostics |
                                              (_points.Any(point => point.Writable) ? DriverCapabilities.Write : DriverCapabilities.None);
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Tag).ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var point in _points) _registry.Upsert(point.Tag);
            if (_runCts is { IsCancellationRequested: false }) return;

            if (!_effectAuthority())
            {
                SetCommunicationState(CommunicationDriverOperationalState.Stopped);
                SetReadiness(CommunicationDriverReadinessState.Stopped);
                Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
                return;
            }

            await MarkAllAsync(TagQuality.Stale, CancellationToken.None).ConfigureAwait(false);
            _transport = _transportFactory() ?? throw new InvalidOperationException("MQTT transport factory returned null.");
            var runCts = new CancellationTokenSource();
            _runCts = runCts;
            _isReady = false;
            SetCommunicationState(CommunicationDriverOperationalState.Starting);
            SetReadiness(CommunicationDriverReadinessState.Starting);
            Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
            _runTask = SuperviseRunAsync(runCts);
            // The committed Runtime lifetime is independent of the short activation request token.
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTag.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Zigbee2MQTT TAG '{tagId}' was not found in driver '{DriverId}'.");
        Interlocked.Increment(ref _reads);
        if (!point.Gettable || !_effectAuthority() || !IsPointActive(tagId) || _transport is not { IsConnected: true } || !_isReady)
        {
            _cache.TryGet(tagId, out var cached);
            return cached;
        }

        try
        {
            return await RequestObservationAsync(point, expectedValue: null, write: false, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _cache.TryGet(tagId, out var current);
            return await PublishQualityAsync(point, current?.Value, TagQuality.Uncertain, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTag.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Zigbee2MQTT TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable) throw new InvalidOperationException($"Zigbee2MQTT TAG '{point.Tag.Path}' is not writable by its declared expose access.");
        if (!IsPointActive(tagId)) throw new InvalidOperationException($"Zigbee2MQTT TAG '{point.Tag.Path}' is not validated against the current bridge inventory.");
        if (!_effectAuthority()) throw new InvalidOperationException("Zigbee2MQTT writes are disabled because this Runtime is not the effective Active authority.");
        if (_transport is not { IsConnected: true } || !_isReady)
            throw new MqttTransportException("Zigbee2MQTT bridge is not connected and ready.");

        var payload = Zigbee2MqttExposeMapper.EncodeSetValue(point, value);
        var pending = new PendingOperation(DateTimeOffset.UtcNow, NormalizeWriteValue(point, value), isWrite: true);
        lock (_stateGate)
        {
            if (_pending.ContainsKey(tagId)) throw new InvalidOperationException($"A Zigbee2MQTT operation is already pending for TAG '{point.Tag.Path}'.");
            _pending[tagId] = pending;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_effectAuthority() || !IsPointActive(tagId))
                    throw new InvalidOperationException("Zigbee2MQTT write authority or current inventory validation was revoked before dispatch.");
                var topic = ResolveSetTopic(point);
                Interlocked.Increment(ref _requests);
                // Exactly one dispatch. A transport timeout is ambiguous and is never retried.
                lock (_stateGate)
                {
                    pending.DispatchedAtUtc = DateTimeOffset.UtcNow;
                    pending.WasDispatched = true;
                }
                await _transport!.PublishAsync(
                    new MqttPublishRequest(topic, Zigbee2MqttEngineeringProvider.BuildSetPayload(point.Property, payload), MqttQosLevel.AtLeastOnce, Retain: false),
                    cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _writes);
            }
            finally { _operationGate.Release(); }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runCts?.Token ?? CancellationToken.None);
            timeout.CancelAfter(_settings.WriteConfirmationTimeout);
            TagValue? confirmation;
            try { confirmation = await pending.Completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !(_runCts?.IsCancellationRequested ?? false))
            {
                Interlocked.Increment(ref _timeouts);
                if (!pending.AmbiguousRecorded)
                {
                    Interlocked.Increment(ref _ambiguousWrites);
                    pending.AmbiguousRecorded = true;
                }
                _cache.TryGet(tagId, out var prior);
                await PublishQualityAsync(point, prior?.Value, TagQuality.Uncertain, CancellationToken.None).ConfigureAwait(false);
                throw new TimeoutException("Zigbee2MQTT write was published but no fresh matching state report confirmed it before timeout.");
            }
            if (confirmation is null || confirmation.Quality != TagQuality.Good)
                throw new IOException("Zigbee2MQTT write did not receive a Good post-dispatch state report.");
        }
        catch (Exception ex)
        {
            if (ex is not InvalidOperationException || pending.WasDispatched)
            {
                Interlocked.Increment(ref _failedOperations);
                if (pending.WasDispatched)
                {
                    if (!pending.AmbiguousRecorded)
                    {
                        Interlocked.Increment(ref _ambiguousWrites);
                        pending.AmbiguousRecorded = true;
                    }
                    _cache.TryGet(tagId, out var prior);
                    await PublishQualityAsync(point, prior?.Value, TagQuality.Uncertain, CancellationToken.None).ConfigureAwait(false);
                }
            }
            throw;
        }
        finally
        {
            lock (_stateGate)
            {
                if (_pending.TryGetValue(tagId, out var current) && ReferenceEquals(current, pending)) _pending.Remove(tagId);
            }
            RecordDuration(Stopwatch.GetElapsedTime(started));
        }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        lock (_stateGate)
        {
            var readinessState = _readinessState;
            if (readinessState == CommunicationDriverReadinessState.Ready &&
                (!_isReady || _transport is not { IsConnected: true } || _bridgeState != "online" || _inventory is null))
            {
                readinessState = CommunicationDriverReadinessState.Starting;
            }

            return new CommunicationDriverReadinessSnapshot(
                DriverId,
                Zigbee2MqttContract.DriverType,
                readinessState,
                DateTimeOffset.UtcNow,
                _lastError,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bridgeState"] = _bridgeState ?? "unknown",
                    ["selectedTagCount"] = _points.Count.ToString(CultureInfo.InvariantCulture),
                    ["subscribedTagCount"] = _activeTagIds.Count.ToString(CultureInfo.InvariantCulture),
                    ["deviceCount"] = (_inventory?.Devices.Count ?? 0).ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var captured = DateTimeOffset.UtcNow;
        lock (_stateGate)
        {
            var durationCount = Interlocked.Read(ref _timedOperations);
            var average = durationCount == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(Interlocked.Read(ref _totalOperationDurationTicks) / durationCount);
            var lastDurationTicks = Interlocked.Read(ref _lastOperationDurationTicks);
            var quality = BuildTagQualitySummary();
            var state = _communicationState;
            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                Zigbee2MqttContract.DriverType,
                _runtimeInstanceId,
                Zigbee2MqttEngineeringProvider.SanitizedEndpoint(_settings),
                state,
                _stateChangedAt,
                captured,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                _lastValueAt.HasValue ? captured - _lastValueAt.Value : null,
                null,
                lastDurationTicks == 0 ? null : TimeSpan.FromTicks(lastDurationTicks),
                average,
                null,
                0d,
                _points.Count,
                quality,
                new CommunicationDriverCounters(
                    Cycles: 0,
                    Requests: Interlocked.Read(ref _requests),
                    SuccessfulOperations: Interlocked.Read(ref _successfulOperations),
                    FailedOperations: Interlocked.Read(ref _failedOperations),
                    ConsecutiveFailures: 0,
                    Timeouts: Interlocked.Read(ref _timeouts),
                    Connections: Interlocked.Read(ref _connections),
                    Disconnections: Interlocked.Read(ref _disconnections),
                    Reconnects: Interlocked.Read(ref _reconnects),
                    ReadOperations: Interlocked.Read(ref _reads),
                    WriteOperations: Interlocked.Read(ref _writes),
                    UpdatesPublished: Interlocked.Read(ref _updates)),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tls"] = _settings.Mqtt.UseTls.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                    ["baseTopic"] = _settings.BaseTopic,
                    ["dataSourceId"] = _dataSourceId.ToString("D", CultureInfo.InvariantCulture),
                    ["bridgeState"] = _bridgeState ?? "unknown",
                    ["deviceCount"] = (_inventory?.Devices.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                    ["retainedMessages"] = Interlocked.Read(ref _retainedMessages).ToString(CultureInfo.InvariantCulture),
                    ["rejectedValues"] = Interlocked.Read(ref _rejectedValues).ToString(CultureInfo.InvariantCulture),
                    ["ambiguousWrites"] = Interlocked.Read(ref _ambiguousWrites).ToString(CultureInfo.InvariantCulture),
                    ["messagesReceived"] = Interlocked.Read(ref _messages).ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    public async ValueTask DisposeAsync()
    {
        Exception? stopError = null;
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { stopError = ex; }

        Exception? disposeError = null;
        var transport = _transport;
        _transport = null;
        if (transport is not null)
        {
            try { await transport.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { disposeError = ex; }
        }
        _disposed = true;
        if (stopError is not null && disposeError is not null)
            throw new AggregateException("Zigbee2MQTT driver stop and transport disposal both failed.", stopError, disposeError);
        if (stopError is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopError).Throw();
        if (disposeError is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(disposeError).Throw();
    }

    private async Task SuperviseRunAsync(CancellationTokenSource runCts)
    {
        try { await RunAsync(runCts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (runCts.IsCancellationRequested) return;
            SetFailure(ex, faulted: true);
            if (_transport is { } transport)
            {
                try { await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = _settings.Mqtt.EffectiveReconnectMinimumDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            var transport = _transport ?? throw new InvalidOperationException("Zigbee2MQTT transport was not created.");
            try
            {
                if (!transport.IsConnected)
                {
                    if (!_effectAuthority())
                    {
                        _isReady = false;
                        SetCommunicationState(CommunicationDriverOperationalState.Stopped);
                        SetReadiness(CommunicationDriverReadinessState.Stopped);
                        await Task.Delay(_settings.Mqtt.EffectiveReconnectMinimumDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    await ConnectAndSynchronizeAsync(transport, cancellationToken).ConfigureAwait(false);
                    delay = _settings.Mqtt.EffectiveReconnectMinimumDelay;
                }

                var message = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or TimeoutException or JsonException or FormatException)
            {
                Interlocked.Increment(ref _failedOperations);
                _isReady = false;
                _bridgeState = null;
                _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
                _lastError = SafeFailure(ex);
                await MarkAllAsync(TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
                SetCommunicationState(CommunicationDriverOperationalState.Reconnecting);
                SetReadiness(CommunicationDriverReadinessState.Starting);
                Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, _lastError, Interlocked.Read(ref _updates));
                if (transport.IsConnected) Interlocked.Increment(ref _disconnections);
                try { await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception disconnectError) { SetFailure(disconnectError, faulted: true); return; }
                _consecutiveConnectFailures++;
                if (ex is MqttTransportException { IsPermanent: true } ||
                    _consecutiveConnectFailures >= _settings.Mqtt.MaximumConsecutiveConnectFailures)
                {
                    SetFailure(ex, faulted: true);
                    return;
                }
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(
                    _settings.Mqtt.EffectiveReconnectMaximumDelay.TotalMilliseconds,
                    Math.Max(_settings.Mqtt.EffectiveReconnectMinimumDelay.TotalMilliseconds, delay.TotalMilliseconds * 2)));
            }
        }
    }

    private async Task ConnectAndSynchronizeAsync(IMqttClientTransport transport, CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) throw new InvalidOperationException("Zigbee2MQTT external effects are not authorized for this Runtime node.");
        using var credentials = await _credentialResolver(cancellationToken).ConfigureAwait(false);
        await transport.ConnectAsync(_settings.Mqtt, credentials, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _connections);
        if (_hasConnectedOnce) Interlocked.Increment(ref _reconnects);
        _hasConnectedOnce = true;
        _consecutiveConnectFailures = 0;
        _sessionStartedAt = DateTimeOffset.UtcNow;
        lock (_stateGate) _freshnessFloorUtc = _sessionStartedAt;
        InvalidateInventory();
        _bridgeState = null;
        _isReady = false;
        SetCommunicationState(CommunicationDriverOperationalState.Starting);

        var probe = await Zigbee2MqttEngineeringProvider.ReadInventoryAsync(transport, _settings, cancellationToken).ConfigureAwait(false);
        await MarkAllAsync(TagQuality.Stale, cancellationToken).ConfigureAwait(false);
        await ApplyInventoryAsync(probe.Inventory, transport, cancellationToken).ConfigureAwait(false);
        _bridgeState = probe.Inventory.BridgeState;
        _isReady = true;
        _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
        SetCommunicationState(CommunicationDriverOperationalState.Healthy);
        SetReadiness(CommunicationDriverReadinessState.Ready);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
    }

    private async Task ApplyInventoryAsync(
        Zigbee2MqttInventory inventory,
        IMqttClientTransport transport,
        CancellationToken cancellationToken)
    {
        var friendly = new Dictionary<string, string>(StringComparer.Ordinal);
        var valid = new HashSet<Guid>();
        var inventoryByIeee = inventory.Devices.ToDictionary(device => device.IeeeAddress, StringComparer.Ordinal);
        foreach (var group in _points.GroupBy(point => point.IeeeAddress, StringComparer.Ordinal))
        {
            if (!inventoryByIeee.TryGetValue(group.Key, out var device))
            {
                foreach (var point in group)
                    await PublishQualityAsync(point, null, TagQuality.BadConfiguration, cancellationToken).ConfigureAwait(false);
                continue;
            }
            friendly[group.Key] = device.FriendlyName;
            foreach (var point in group)
            {
                try
                {
                    var expose = Zigbee2MqttEngineeringProvider.RequireCurrentExpose(device, point);
                    Zigbee2MqttEngineeringProvider.ValidateExposeAgainstPoint(expose, point, point.Tag.DataType, point.Tag.EngineeringUnit);
                    valid.Add(point.Tag.Id);
                }
                catch (Exception)
                {
                    await PublishQualityAsync(point, null, TagQuality.BadConfiguration, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var stateTopics = new Dictionary<string, string>(StringComparer.Ordinal);
        var availabilityTopics = new Dictionary<string, string>(StringComparer.Ordinal);
        var subscriptions = new Dictionary<string, MqttSubscription>(StringComparer.Ordinal);
        foreach (var ieee in valid.Select(tagId => _pointsByTag[tagId].IeeeAddress).Distinct(StringComparer.Ordinal))
        {
            if (!friendly.TryGetValue(ieee, out var friendlyName)) continue;
            var stateTopic = Zigbee2MqttEngineeringProvider.StateTopic(_settings, friendlyName);
            var availabilityTopic = Zigbee2MqttEngineeringProvider.AvailabilityTopic(_settings, friendlyName);
            MqttPoint.ValidateExactTopic(stateTopic, nameof(stateTopic));
            MqttPoint.ValidateExactTopic(availabilityTopic, nameof(availabilityTopic));
            subscriptions[stateTopic] = new MqttSubscription(stateTopic, MqttQosLevel.AtLeastOnce);
            subscriptions[availabilityTopic] = new MqttSubscription(availabilityTopic, MqttQosLevel.AtLeastOnce);
            stateTopics[stateTopic] = ieee;
            availabilityTopics[availabilityTopic] = ieee;
        }
        lock (_stateGate)
        {
            _inventory = inventory;
            _friendlyNameByIeee = friendly;
            _activeTagIds = valid;
            _ieeeByStateTopic = stateTopics;
            _ieeeByAvailabilityTopic = availabilityTopics;
        }
        if (subscriptions.Count > 0)
        {
            Interlocked.Add(ref _requests, subscriptions.Count);
            await transport.SubscribeAsync(subscriptions.Values.ToArray(), cancellationToken).ConfigureAwait(false);
            Interlocked.Add(ref _successfulOperations, subscriptions.Count);
        }
    }

    private async Task HandleMessageAsync(MqttTransportMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _messages);
        if (message.Retained) Interlocked.Increment(ref _retainedMessages);
        Interlocked.Increment(ref _requests);

        if (message.Topic == _settings.BridgeStateTopic)
        {
            _bridgeState = NormalizeState(message.Payload);
            if (_bridgeState is "online" or "offline") AdvanceFreshnessFloor(message.ReceivedAtUtc);
            InvalidateInventory();
            _isReady = false;
            SetReadiness(CommunicationDriverReadinessState.Starting);
            SetCommunicationState(CommunicationDriverOperationalState.Degraded);
            if (_bridgeState == "offline")
            {
                await MarkAllAsync(TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
            }
            else if (_bridgeState == "online")
            {
                await MarkAllAsync(TagQuality.Stale, cancellationToken).ConfigureAwait(false);
                await _transport!.SubscribeAsync([new MqttSubscription(_settings.BridgeDevicesTopic, MqttQosLevel.AtLeastOnce)], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await MarkAllAsync(TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        if (message.Topic == _settings.BridgeDevicesTopic)
        {
            try
            {
                var inventory = Zigbee2MqttExposeMapper.ParseInventory(message.Payload, _settings, _bridgeState);
                if (inventory.BridgeState == "online")
                {
                    await ApplyInventoryAsync(inventory, _transport!, cancellationToken).ConfigureAwait(false);
                    _bridgeState = inventory.BridgeState;
                    AdvanceFreshnessFloor(message.ReceivedAtUtc);
                    _isReady = true;
                    _lastError = null;
                    _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
                    SetCommunicationState(CommunicationDriverOperationalState.Healthy);
                    SetReadiness(CommunicationDriverReadinessState.Ready);
                }
                else
                {
                    _isReady = false;
                    InvalidateInventory();
                    SetCommunicationState(CommunicationDriverOperationalState.Degraded);
                    SetReadiness(CommunicationDriverReadinessState.Starting);
                    await MarkAllAsync(TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _lastError = SafeFailure(ex);
                _isReady = false;
                InvalidateInventory();
                SetCommunicationState(CommunicationDriverOperationalState.Degraded);
                SetReadiness(CommunicationDriverReadinessState.Starting);
                await MarkAllAsync(TagQuality.BadConfiguration, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        string? availabilityIeee;
        lock (_stateGate)
            _ieeeByAvailabilityTopic.TryGetValue(message.Topic, out availabilityIeee);
        if (availabilityIeee is not null)
        {
            var availability = NormalizeState(message.Payload);
            if (availability == "online")
            {
                await MarkDeviceAsync(availabilityIeee, TagQuality.Stale, cancellationToken).ConfigureAwait(false);
            }
            else if (availability == "offline")
            {
                await MarkDeviceAsync(availabilityIeee, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        string? ieee;
        Zigbee2MqttPoint[] statePoints;
        lock (_stateGate)
        {
            _ieeeByStateTopic.TryGetValue(message.Topic, out ieee);
            statePoints = ieee is null
                ? []
                : _points.Where(point => point.IeeeAddress == ieee && _activeTagIds.Contains(point.Tag.Id)).ToArray();
        }
        if (ieee is null) return;
        if (message.Payload.Length > _settings.Mqtt.MaximumInboundPayloadBytes)
        {
            await MarkDeviceAsync(ieee, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _rejectedValues);
            _lastError = "Zigbee2MQTT device state payload exceeded the configured size limit.";
            foreach (var point in statePoints) CompletePendingReadWithFailure(point, message);
            return;
        }

        JsonDocument document;
        try { document = JsonDocument.Parse(message.Payload, new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException ex)
        {
            await MarkDeviceAsync(ieee, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _rejectedValues);
            _lastError = SafeFailure(ex);
            foreach (var point in statePoints) CompletePendingReadWithFailure(point, message);
            return;
        }

        using (document)
        {
            foreach (var point in statePoints)
            {
                if (!Zigbee2MqttExposeMapper.TryDecodeValue(point, document.RootElement, out var value, out _))
                {
                    Interlocked.Increment(ref _rejectedValues);
                    _cache.TryGet(point.Tag.Id, out var badPrevious);
                    var badValue = await PublishQualityAsync(point, badPrevious?.Value, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
                    CompletePendingReadWithFailure(point, message, badValue);
                    continue;
                }

                PendingOperation? pending;
                DateTimeOffset? pendingDispatchedAt;
                DateTimeOffset freshnessFloorUtc;
                lock (_stateGate)
                {
                    _pending.TryGetValue(point.Tag.Id, out pending);
                    pendingDispatchedAt = pending?.DispatchedAtUtc;
                    freshnessFloorUtc = _freshnessFloorUtc;
                }

                var quality = ResolveObservationQuality(message, pendingDispatchedAt, freshnessFloorUtc, document.RootElement);
                var update = await PublishQualityAsync(point, value, quality, cancellationToken).ConfigureAwait(false);
                if (quality == TagQuality.Good)
                {
                    _lastValueAt = message.ReceivedAtUtc;
                    _lastSuccessfulCommunicationAt = message.ReceivedAtUtc;
                    Interlocked.Increment(ref _successfulOperations);
                }
                else if (quality is TagQuality.BadDevice or TagQuality.Uncertain or TagQuality.Stale)
                {
                    Interlocked.Increment(ref _rejectedValues);
                }

                if (pending is not null && pendingDispatchedAt is { } dispatchedAt &&
                    message.ReceivedAtUtc >= dispatchedAt && !message.Retained)
                {
                    if (!pending.IsWrite || (quality == TagQuality.Good && ValuesEqual(value, pending.ExpectedValue)))
                        pending.Completion.TrySetResult(update);
                }
            }
        }
    }

    private TagQuality ResolveObservationQuality(
        MqttTransportMessage message,
        DateTimeOffset? pendingDispatchedAt,
        DateTimeOffset freshnessFloorUtc,
        JsonElement state)
    {
        if (message.Retained) return TagQuality.Stale;
        if (_bridgeState == "offline") return TagQuality.BadCommunication;
        if (pendingDispatchedAt is { } dispatchedAt)
        {
            // A report received after dispatch is not enough when its own
            // source timestamp proves it was observed before the operation.
            if (message.ReceivedAtUtc < dispatchedAt) return TagQuality.Uncertain;
            if (dispatchedAt > freshnessFloorUtc) freshnessFloorUtc = dispatchedAt;
        }

        // Receive time and retain=false only describe MQTT delivery. A Good
        // value needs source-side evidence newer than this broker/bridge epoch
        // and, for an in-flight operation, newer than that operation's dispatch.
        return TryGetSourceLastSeen(state, out var sourceTime) && sourceTime > freshnessFloorUtc
            ? TagQuality.Good
            : TagQuality.Uncertain;
    }

    private void AdvanceFreshnessFloor(DateTimeOffset timestamp)
    {
        lock (_stateGate)
        {
            if (timestamp > _freshnessFloorUtc) _freshnessFloorUtc = timestamp;
        }
    }

    private void InvalidateInventory()
    {
        lock (_stateGate)
        {
            _inventory = null;
            _friendlyNameByIeee = new Dictionary<string, string>(StringComparer.Ordinal);
            _ieeeByStateTopic = new Dictionary<string, string>(StringComparer.Ordinal);
            _ieeeByAvailabilityTopic = new Dictionary<string, string>(StringComparer.Ordinal);
            _activeTagIds = new HashSet<Guid>();
        }
    }

    private void CompletePendingReadWithFailure(Zigbee2MqttPoint point, MqttTransportMessage message, TagValue? failedValue = null)
    {
        if (message.Retained) return;
        lock (_stateGate)
        {
            if (!_pending.TryGetValue(point.Tag.Id, out var pending) || pending.IsWrite ||
                pending.DispatchedAtUtc is not { } dispatchedAt || message.ReceivedAtUtc < dispatchedAt)
                return;
            if (failedValue is null) _cache.TryGet(point.Tag.Id, out failedValue);
            pending.Completion.TrySetResult(failedValue);
        }
    }

    private async ValueTask<TagValue> RequestObservationAsync(
        Zigbee2MqttPoint point,
        object? expectedValue,
        bool write,
        CancellationToken cancellationToken)
    {
        var pending = new PendingOperation(DateTimeOffset.UtcNow, expectedValue, write);
        lock (_stateGate)
        {
            if (_pending.ContainsKey(point.Tag.Id)) throw new InvalidOperationException($"A Zigbee2MQTT operation is already pending for TAG '{point.Tag.Path}'.");
            _pending[point.Tag.Id] = pending;
        }
        try
        {
            var friendly = ResolveFriendlyName(point);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runCts?.Token ?? CancellationToken.None);
            timeout.CancelAfter(_settings.RequestTimeout);
            await _operationGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                if (!_effectAuthority() || !IsPointActive(point.Tag.Id))
                    throw new InvalidOperationException("Zigbee2MQTT read authority or current inventory validation was revoked before dispatch.");
                Interlocked.Increment(ref _requests);
                lock (_stateGate) pending.DispatchedAtUtc = DateTimeOffset.UtcNow;
                await _transport!.PublishAsync(
                    new MqttPublishRequest(
                        Zigbee2MqttEngineeringProvider.GetTopic(_settings, friendly),
                        Zigbee2MqttEngineeringProvider.BuildGetPayload(point.Property),
                        MqttQosLevel.AtLeastOnce,
                        Retain: false),
                    timeout.Token).ConfigureAwait(false);
                if (write) Interlocked.Increment(ref _writes); else Interlocked.Increment(ref _reads);
            }
            finally { _operationGate.Release(); }
            try { return await pending.Completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false) ?? throw new TimeoutException(); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !(_runCts?.IsCancellationRequested ?? false))
            {
                Interlocked.Increment(ref _timeouts);
                throw new TimeoutException("Zigbee2MQTT returned no fresh non-retained state report before the PointRead timeout.");
            }
        }
        finally
        {
            lock (_stateGate)
            {
                if (_pending.TryGetValue(point.Tag.Id, out var current) && ReferenceEquals(current, pending)) _pending.Remove(point.Tag.Id);
            }
        }
    }

    private string ResolveSetTopic(Zigbee2MqttPoint point) =>
        Zigbee2MqttEngineeringProvider.SetTopic(_settings, ResolveFriendlyName(point));

    private bool IsPointActive(Guid tagId)
    {
        lock (_stateGate) return _activeTagIds.Contains(tagId);
    }

    private string ResolveFriendlyName(Zigbee2MqttPoint point)
    {
        lock (_stateGate)
        {
            if (_friendlyNameByIeee.TryGetValue(point.IeeeAddress, out var friendly)) return friendly;
        }
        throw new InvalidOperationException($"IEEE device '{point.IeeeAddress}' is not present in the current bridge inventory.");
    }

    private async Task MarkDeviceAsync(string ieee, TagQuality quality, CancellationToken cancellationToken)
    {
        foreach (var point in _points.Where(point => point.IeeeAddress == ieee))
        {
            _cache.TryGet(point.Tag.Id, out var previous);
            await PublishQualityAsync(point, previous?.Value, quality, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MarkAllAsync(TagQuality quality, CancellationToken cancellationToken)
    {
        foreach (var point in _points)
        {
            _cache.TryGet(point.Tag.Id, out var previous);
            await PublishQualityAsync(point, previous?.Value, quality, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<TagValue> PublishQualityAsync(
        Zigbee2MqttPoint point,
        object? value,
        TagQuality quality,
        CancellationToken cancellationToken)
    {
        var tagValue = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, tagValue, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updates);
        return tagValue;
    }

    private CommunicationTagQualitySummary BuildTagQualitySummary()
    {
        var counts = new Dictionary<TagQuality, int>();
        var noSample = 0;
        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var value) || value is null) { noSample++; continue; }
            counts[value.Quality] = counts.GetValueOrDefault(value.Quality) + 1;
        }
        return new CommunicationTagQualitySummary(
            counts.GetValueOrDefault(TagQuality.Good),
            counts.GetValueOrDefault(TagQuality.BadCommunication),
            counts.GetValueOrDefault(TagQuality.Uncertain),
            counts.GetValueOrDefault(TagQuality.Bad) + counts.GetValueOrDefault(TagQuality.Unavailable),
            counts.GetValueOrDefault(TagQuality.BadConfiguration),
            counts.GetValueOrDefault(TagQuality.BadDevice),
            counts.GetValueOrDefault(TagQuality.Stale),
            counts.GetValueOrDefault(TagQuality.Disabled),
            noSample);
    }

    private static bool TryGetSourceLastSeen(JsonElement state, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (state.ValueKind != JsonValueKind.Object || !state.TryGetProperty("last_seen", out var value)) return false;
        if (value.ValueKind == JsonValueKind.String)
            return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            try
            {
                timestamp = number > 10_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(number)
                    : DateTimeOffset.FromUnixTimeSeconds(number);
                return true;
            }
            catch (ArgumentOutOfRangeException) { return false; }
        }
        return false;
    }

    private static string? NormalizeState(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty || payload.Length > 4096) return null;
        var text = Encoding.UTF8.GetString(payload.Span).Trim();
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind == JsonValueKind.String)
                text = document.RootElement.GetString() ?? string.Empty;
            else if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String)
                text = state.GetString() ?? string.Empty;
        }
        catch (JsonException) { }
        return text.Equals("online", StringComparison.OrdinalIgnoreCase) ? "online"
            : text.Equals("offline", StringComparison.OrdinalIgnoreCase) ? "offline"
            : null;
    }

    private static bool ValuesEqual(object? left, object? right) =>
        left switch
        {
            double number when right is not null => right switch
            {
                double value => number.Equals(value),
                float value => number.Equals((double)value),
                decimal value => (decimal)number == value,
                _ => false
            },
            _ => Equals(left, right)
        };

    private static object? NormalizeWriteValue(Zigbee2MqttPoint point, object? value)
    {
        if (point.ValueKind == Zigbee2MqttValueKind.Boolean) return value;
        return value switch
        {
            double number => number,
            float number => (double)number,
            decimal number => (double)number,
            byte number => (double)number,
            sbyte number => (double)number,
            short number => (double)number,
            ushort number => (double)number,
            int number => (double)number,
            uint number => (double)number,
            long number => (double)number,
            ulong number => (double)number,
            _ => value
        };
    }

    private void SetCommunicationState(CommunicationDriverOperationalState state)
    {
        lock (_stateGate)
        {
            if (_communicationState == state) return;
            _communicationState = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private void SetReadiness(CommunicationDriverReadinessState state)
    {
        lock (_stateGate) _readinessState = state;
    }

    private void SetFailure(Exception exception, bool faulted)
    {
        _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
        _lastError = SafeFailure(exception);
        SetCommunicationState(faulted ? CommunicationDriverOperationalState.Faulted : CommunicationDriverOperationalState.Degraded);
        SetReadiness(faulted ? CommunicationDriverReadinessState.Faulted : CommunicationDriverReadinessState.Starting);
        Status = new DriverStatus(DriverId, Name, faulted ? DriverState.Faulted : DriverState.Running, DateTimeOffset.UtcNow, _lastError, Interlocked.Read(ref _updates));
    }

    private static string SafeFailure(Exception exception) => exception switch
    {
        TimeoutException => "MQTT or Zigbee2MQTT did not respond before the configured timeout.",
        JsonException or FormatException => "Zigbee2MQTT sent malformed or unsupported bridge/state data.",
        MqttTransportException => "MQTT broker connection, subscription, or transport operation failed.",
        _ => "Zigbee2MQTT runtime operation failed; inspect sanitized #500 communication diagnostics."
    };

    private void RecordDuration(TimeSpan duration)
    {
        Interlocked.Exchange(ref _lastOperationDurationTicks, duration.Ticks);
        Interlocked.Add(ref _totalOperationDurationTicks, duration.Ticks);
        Interlocked.Increment(ref _timedOperations);
    }

    private async Task StopCoreAsync()
    {
        var cts = _runCts;
        var task = _runTask;
        if (cts is null)
        {
            SetCommunicationState(CommunicationDriverOperationalState.Stopped);
            SetReadiness(CommunicationDriverReadinessState.Stopped);
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
            return;
        }

        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
        SetCommunicationState(CommunicationDriverOperationalState.Stopping);
        await cts.CancelAsync().ConfigureAwait(false);
        Exception? stopError = null;
        try
        {
            if (task is not null) await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception ex) { stopError = ex; }

        var transport = _transport;
        if (transport is not null)
        {
            try { await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { stopError = stopError is null ? ex : new AggregateException(stopError, ex); }
            try { await transport.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { stopError = stopError is null ? ex : new AggregateException(stopError, ex); }
        }
        _transport = null;
        _runCts = null;
        _runTask = null;
        cts.Dispose();
        _isReady = false;
        lock (_stateGate)
        {
            _activeTagIds.Clear();
            foreach (var pending in _pending.Values) pending.Completion.TrySetCanceled();
            _pending.Clear();
        }

        if (stopError is not null)
        {
            _lastError = SafeFailure(stopError);
            SetCommunicationState(CommunicationDriverOperationalState.Faulted);
            SetReadiness(CommunicationDriverReadinessState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, _lastError, Interlocked.Read(ref _updates));
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopError).Throw();
        }

        await MarkAllAsync(TagQuality.Stale, CancellationToken.None).ConfigureAwait(false);
        SetCommunicationState(CommunicationDriverOperationalState.Stopped);
        SetReadiness(CommunicationDriverReadinessState.Stopped);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class PendingOperation(DateTimeOffset requestedAt, object? expectedValue, bool isWrite)
    {
        public DateTimeOffset RequestedAt { get; } = requestedAt;
        public object? ExpectedValue { get; } = expectedValue;
        public bool IsWrite { get; } = isWrite;
        public DateTimeOffset? DispatchedAtUtc { get; set; }
        public bool WasDispatched { get; set; }
        public bool AmbiguousRecorded { get; set; }
        public TaskCompletionSource<TagValue?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
