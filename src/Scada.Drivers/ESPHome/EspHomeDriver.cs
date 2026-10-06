using System.Diagnostics;
using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.ESPHome;

public sealed class EspHomeDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource
{
    private sealed record PendingWrite(object? ExpectedValue, TaskCompletionSource<TagValue> Completion);

    private readonly EspHomeConnectionSettings _settings;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyCollection<EspHomePoint> _points;
    private readonly IReadOnlyDictionary<Guid, EspHomePoint> _pointsById;
    private readonly IReadOnlyDictionary<EspHomeEntityAddress, EspHomePoint> _pointsByAddress;
    private readonly IEspHomeNativeClient _client;
    private readonly Dictionary<Guid, PendingWrite> _pendingWrites = new();
    private readonly HashSet<Guid> _missingConfiguredTagIds = new();
    private readonly object _gate = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private bool _disposed;

    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _lastFailure;
    private DateTimeOffset? _lastStateMessage;
    private DateTimeOffset? _lastSuccessfulCommand;
    private DateTimeOffset? _lastInventoryReconciliation;
    private DateTimeOffset? _lastPingSent;
    private TimeSpan? _lastOperationDuration;
    private double _averageOperationMilliseconds;
    private long _operationDurationSamples;
    private string? _lastError;
    private string? _baselineStableIdentity;
    private EspHomeNativeInventory? _inventory;
    private bool _expectedOffline;

    private long _cycles;
    private long _requests;
    private long _successful;
    private long _failed;
    private long _consecutiveFailures;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private long _reads;
    private long _writes;
    private long _updates;

    public EspHomeDriver(
        string driverId,
        string name,
        EspHomeConnectionSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IReadOnlyCollection<EspHomePoint> points,
        IEspHomeNativeClient client)
    {
        DriverId = driverId;
        Name = name;
        _settings = settings;
        _cache = cache;
        _registry = registry;
        _points = points;
        _pointsById = points.ToDictionary(x => x.Tag.Id);
        _pointsByAddress = points.ToDictionary(x => x.Address);
        _client = client;
        Tags = points.Select(x => x.Tag).ToArray();
        Status = new DriverStatus(driverId, name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags { get; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_runTask is not null) return;
        _settings.Validate();
        foreach (var point in _points) _registry.Upsert(point.Tag);

        Transition(CommunicationDriverOperationalState.Starting);
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            try
            {
                await ConnectAndSubscribeAsync(isReconnect: false, _runCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                _settings.DeepSleepPolicy == EspHomeDeepSleepPolicy.Expected &&
                ex is not OperationCanceledException &&
                ex is not EspHomeDeviceIdentityChangedException)
            {
                RecordFailure(ex, null);
                await SafeDisconnectAsync().ConfigureAwait(false);
                lock (_gate) _expectedOffline = true;
                Transition(CommunicationDriverOperationalState.Reconnecting);
                await MarkUnavailableAsync(TagQuality.Unavailable, _runCts.Token).ConfigureAwait(false);
            }

            _runTask = RunManagedSessionAsync(_runCts.Token);
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Running,
                DateTimeOffset.UtcNow,
                CurrentStatusMessage(),
                Interlocked.Read(ref _updates));
        }
        catch (Exception ex)
        {
            RecordFailure(ex, null);
            await SafeDisconnectAsync().ConfigureAwait(false);
            Transition(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Faulted,
                DateTimeOffset.UtcNow,
                Sanitize(ex.Message),
                Interlocked.Read(ref _updates));
            _runCts.Dispose();
            _runCts = null;
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _runCts;
        if (cts is null) return;

        Transition(CommunicationDriverOperationalState.Stopping);
        cts.Cancel();
        FailPendingWrites(new OperationCanceledException("ESPHome driver is stopping."));
        await _client.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _runTask = null;
        _runCts = null;
        cts.Dispose();
        lock (_gate) _expectedOffline = false;
        Transition(CommunicationDriverOperationalState.Stopped);
        Status = new DriverStatus(
            DriverId,
            Name,
            DriverState.Stopped,
            DateTimeOffset.UtcNow,
            UpdatesPublished: Interlocked.Read(ref _updates));
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsById.ContainsKey(tagId))
            throw new KeyNotFoundException($"ESPHome TAG '{tagId}' is not owned by this driver.");

        Interlocked.Increment(ref _reads);
        return ValueTask.FromResult(_cache.TryGet(tagId, out var value) ? value : null);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_pointsById.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"ESPHome TAG '{tagId}' is not owned by this driver.");
        if (!point.CanWrite)
            throw new InvalidOperationException($"ESPHome TAG '{point.Tag.Path}' is read-only.");

        lock (_gate)
        {
            if (_missingConfiguredTagIds.Contains(tagId))
                throw new InvalidOperationException(
                    $"ESPHome TAG '{point.Tag.Path}' is not present in the current device inventory.");
            if (_communicationState is CommunicationDriverOperationalState.Reconnecting
                or CommunicationDriverOperationalState.Faulted
                or CommunicationDriverOperationalState.Stopped
                or CommunicationDriverOperationalState.Stopping)
                throw new InvalidOperationException(
                    $"ESPHome TAG '{point.Tag.Path}' cannot be written while driver state is '{_communicationState}'.");
        }

        if (!_client.Connected)
            throw new InvalidOperationException($"ESPHome TAG '{point.Tag.Path}' cannot be written while the device is offline.");

        var expected = ConvertWriteValue(point.Tag.DataType, value);
        var completion = new TaskCompletionSource<TagValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pendingWrites.ContainsKey(tagId))
                throw new InvalidOperationException(
                    $"ESPHome TAG '{point.Tag.Path}' already has a pending authoritative write reconciliation.");
            _pendingWrites[tagId] = new PendingWrite(expected, completion);
        }

        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            await _client.SendCommandAsync(
                new EspHomeCommand(point.Address, point.WriteKind, expected),
                cancellationToken).ConfigureAwait(false);

            try
            {
                _ = await completion.Task
                    .WaitAsync(_settings.EffectiveWriteReconcileTimeout, cancellationToken)
                    .ConfigureAwait(false);
                lock (_gate) _lastSuccessfulCommand = DateTimeOffset.UtcNow;
                RecordSuccess(Stopwatch.GetElapsedTime(started));
            }
            catch (TimeoutException timeout)
            {
                Interlocked.Increment(ref _timeouts);
                RecordFailure(timeout, Stopwatch.GetElapsedTime(started));
                await MarkWriteUncertainAsync(point, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException(
                    $"ESPHome command for TAG '{point.Tag.Path}' was sent but no matching authoritative state arrived within {_settings.EffectiveWriteReconcileTimeout.TotalMilliseconds:0} ms.");
            }
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            RecordFailure(ex, Stopwatch.GetElapsedTime(started));
            throw;
        }
        finally
        {
            lock (_gate) _pendingWrites.Remove(tagId);
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        CommunicationDriverOperationalState state;
        DateTimeOffset stateChanged;
        DateTimeOffset? lastSuccess;
        DateTimeOffset? lastFailure;
        DateTimeOffset? lastState;
        DateTimeOffset? lastCommand;
        DateTimeOffset? lastInventory;
        DateTimeOffset? lastPing;
        TimeSpan? lastDuration;
        double averageDurationMs;
        string? lastError;
        string? stableIdentity;
        EspHomeNativeInventory? inventory;
        int missingCount;
        bool expectedOffline;

        lock (_gate)
        {
            state = _communicationState;
            stateChanged = _stateChangedAt;
            lastSuccess = _lastSuccess;
            lastFailure = _lastFailure;
            lastState = _lastStateMessage;
            lastCommand = _lastSuccessfulCommand;
            lastInventory = _lastInventoryReconciliation;
            lastPing = _lastPingSent;
            lastDuration = _lastOperationDuration;
            averageDurationMs = _averageOperationMilliseconds;
            lastError = _lastError;
            stableIdentity = _baselineStableIdentity;
            inventory = _inventory;
            missingCount = _missingConfiguredTagIds.Count;
            expectedOffline = _expectedOffline;
        }

        var successful = Interlocked.Read(ref _successful);
        var failed = Interlocked.Read(ref _failed);
        var total = successful + failed;

        return new CommunicationDriverDiagnosticSnapshot(
            DriverId,
            Name,
            EspHomeNativeContract.DriverType,
            _runtimeInstanceId,
            _settings.SanitizedEndpoint,
            state,
            stateChanged,
            DateTimeOffset.UtcNow,
            lastSuccess,
            lastFailure,
            lastError,
            lastState.HasValue ? DateTimeOffset.UtcNow - lastState.Value : null,
            null,
            lastDuration,
            averageDurationMs > 0 ? TimeSpan.FromMilliseconds(averageDurationMs) : null,
            null,
            total == 0 ? 0d : (double)failed / total,
            _points.Count,
            BuildQualitySummary(),
            new CommunicationDriverCounters(
                Cycles: Interlocked.Read(ref _cycles),
                Requests: Interlocked.Read(ref _requests),
                SuccessfulOperations: successful,
                FailedOperations: failed,
                ConsecutiveFailures: Interlocked.Read(ref _consecutiveFailures),
                Timeouts: Interlocked.Read(ref _timeouts),
                Connections: Interlocked.Read(ref _connections),
                Disconnections: Interlocked.Read(ref _disconnections),
                Reconnects: Interlocked.Read(ref _reconnects),
                ReadOperations: Interlocked.Read(ref _reads),
                WriteOperations: Interlocked.Read(ref _writes),
                UpdatesPublished: Interlocked.Read(ref _updates)),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stableDeviceIdentity"] = stableIdentity ?? string.Empty,
                ["apiVersion"] = inventory?.NegotiatedVersion.ToString() ?? string.Empty,
                ["firmware"] = inventory?.Device.ESPHomeVersion ?? string.Empty,
                ["model"] = inventory?.Device.Model ?? string.Empty,
                ["encryptionMode"] = _settings.EncryptionMode.ToString().ToLowerInvariant(),
                ["transportSecurity"] = _settings.EncryptionMode == EspHomeNativeEncryptionMode.Noise
                    ? "noise-encrypted"
                    : "plaintext-insecure-explicit",
                ["connected"] = _client.Connected.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["entityCount"] = inventory?.Entities.Count.ToString(CultureInfo.InvariantCulture) ?? "0",
                ["unsupportedEntityCount"] = inventory?.UnsupportedEntityMessageCount.ToString(CultureInfo.InvariantCulture) ?? "0",
                ["missingConfiguredEntityCount"] = missingCount.ToString(CultureInfo.InvariantCulture),
                ["lastStateMessage"] = lastState?.ToString("O") ?? string.Empty,
                ["lastSuccessfulCommand"] = lastCommand?.ToString("O") ?? string.Empty,
                ["lastInventoryReconciliation"] = lastInventory?.ToString("O") ?? string.Empty,
                ["lastPingSent"] = lastPing?.ToString("O") ?? string.Empty,
                ["deepSleepPolicy"] = _settings.DeepSleepPolicy.ToString().ToLowerInvariant(),
                ["deviceReportsDeepSleep"] = (inventory?.Device.HasDeepSleep ?? false)
                    .ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["expectedOffline"] = expectedOffline.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["reconnectMinimumMilliseconds"] = _settings.EffectiveReconnectMinimumDelay.TotalMilliseconds
                    .ToString("0", CultureInfo.InvariantCulture),
                ["reconnectMaximumMilliseconds"] = _settings.EffectiveReconnectMaximumDelay.TotalMilliseconds
                    .ToString("0", CultureInfo.InvariantCulture)
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunManagedSessionAsync(CancellationToken cancellationToken)
    {
        var reconnectDelay = _settings.EffectiveReconnectMinimumDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_client.Connected)
            {
                try
                {
                    await DelayReconnectAsync(reconnectDelay, cancellationToken).ConfigureAwait(false);
                    await ConnectAndSubscribeAsync(isReconnect: true, cancellationToken).ConfigureAwait(false);
                    reconnectDelay = _settings.EffectiveReconnectMinimumDelay;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (EspHomeDeviceIdentityChangedException identityError)
                {
                    await EnterFatalStateAsync(identityError, cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (Exception reconnectError)
                {
                    if (reconnectError is TimeoutException)
                        Interlocked.Increment(ref _timeouts);
                    RecordFailure(reconnectError, null);
                    await SafeDisconnectAsync().ConfigureAwait(false);
                    Transition(CommunicationDriverOperationalState.Reconnecting);
                    Status = new DriverStatus(
                        DriverId,
                        Name,
                        DriverState.Running,
                        DateTimeOffset.UtcNow,
                        CurrentStatusMessage(),
                        Interlocked.Read(ref _updates));
                    reconnectDelay = NextReconnectDelay(reconnectDelay);
                }

                continue;
            }

            try
            {
                await RunConnectedSessionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (EspHomeDeviceIdentityChangedException identityError)
            {
                await EnterFatalStateAsync(identityError, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    Interlocked.Increment(ref _timeouts);
                RecordFailure(ex, null);
                Interlocked.Increment(ref _disconnections);
                FailPendingWrites(ex);
                lock (_gate) _expectedOffline = _settings.DeepSleepPolicy == EspHomeDeepSleepPolicy.Expected;
                Transition(CommunicationDriverOperationalState.Reconnecting);
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Running,
                    DateTimeOffset.UtcNow,
                    CurrentStatusMessage(),
                    Interlocked.Read(ref _updates));
                await MarkUnavailableAsync(
                    _settings.DeepSleepPolicy == EspHomeDeepSleepPolicy.Expected
                        ? TagQuality.Unavailable
                        : TagQuality.BadCommunication,
                    cancellationToken).ConfigureAwait(false);
                await SafeDisconnectAsync().ConfigureAwait(false);
                reconnectDelay = _settings.EffectiveReconnectMinimumDelay;
            }
        }
    }

    private async Task RunConnectedSessionAsync(CancellationToken cancellationToken)
    {
        var receiveTask = _client.ReceiveStateAsync(cancellationToken).AsTask();

        while (!cancellationToken.IsCancellationRequested)
        {
            var pingDelay = Task.Delay(_settings.EffectiveKeepAliveInterval, cancellationToken);
            var completed = await Task.WhenAny(receiveTask, pingDelay).ConfigureAwait(false);

            if (completed == pingDelay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref _requests);
                await _client.SendPingAsync(cancellationToken).ConfigureAwait(false);
                lock (_gate) _lastPingSent = DateTimeOffset.UtcNow;
                continue;
            }

            var update = await receiveTask.ConfigureAwait(false);
            await PublishStateAsync(update, cancellationToken).ConfigureAwait(false);
            receiveTask = _client.ReceiveStateAsync(cancellationToken).AsTask();
        }
    }

    private async Task ConnectAndSubscribeAsync(bool isReconnect, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _requests);
        var inventory = await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        string? baseline;
        lock (_gate) baseline = _baselineStableIdentity;
        if (baseline is not null &&
            !string.Equals(baseline, inventory.Device.StableDeviceIdentity, StringComparison.OrdinalIgnoreCase))
            throw new EspHomeDeviceIdentityChangedException(baseline, inventory.Device.StableDeviceIdentity);

        if (baseline is null)
            lock (_gate) _baselineStableIdentity = inventory.Device.StableDeviceIdentity;

        await _client.SubscribeStatesAsync(cancellationToken).ConfigureAwait(false);
        var missing = ReconcileInventory(inventory);
        if (missing.Count > 0)
            await MarkMissingEntityTagsAsync(missing, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref _connections);
        if (isReconnect) Interlocked.Increment(ref _reconnects);
        lock (_gate)
        {
            _inventory = inventory;
            _lastInventoryReconciliation = DateTimeOffset.UtcNow;
            _expectedOffline = false;
        }

        RecordSuccess(Stopwatch.GetElapsedTime(started));
        Transition(missing.Count == 0
            ? CommunicationDriverOperationalState.Healthy
            : CommunicationDriverOperationalState.Degraded);
        Status = new DriverStatus(
            DriverId,
            Name,
            DriverState.Running,
            DateTimeOffset.UtcNow,
            missing.Count == 0 ? null : $"{missing.Count} configured ESPHome TAG(s) are absent from current inventory.",
            Interlocked.Read(ref _updates));
    }

    private HashSet<Guid> ReconcileInventory(EspHomeNativeInventory inventory)
    {
        var presentEntities = inventory.Entities
            .Select(x => (x.Kind, x.DeviceId, x.Key))
            .ToHashSet();
        var missing = _points
            .Where(point => !presentEntities.Contains((point.Address.Kind, point.Address.DeviceId, point.Address.Key)))
            .Select(point => point.Tag.Id)
            .ToHashSet();

        lock (_gate)
        {
            _missingConfiguredTagIds.Clear();
            foreach (var tagId in missing)
                _missingConfiguredTagIds.Add(tagId);
        }

        return missing;
    }

    private async Task PublishStateAsync(EspHomeStateUpdate update, CancellationToken cancellationToken)
    {
        if (!_pointsByAddress.TryGetValue(update.Address, out var point))
            return;

        _cache.TryGet(point.Tag.Id, out var previous);
        var quality = update.MissingState ? TagQuality.Uncertain : TagQuality.Good;
        var value = update.MissingState ? previous?.Value : ConvertStateValue(point.Tag.DataType, update.Value);
        var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId)
        {
            SourceTimestamp = update.SourceTimestamp
        };
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _cycles);
        Interlocked.Increment(ref _updates);

        lock (_gate)
        {
            _lastStateMessage = DateTimeOffset.UtcNow;
            _expectedOffline = false;
        }
        RecordSuccess(null);

        if (quality == TagQuality.Good)
        {
            PendingWrite? pending;
            lock (_gate) _pendingWrites.TryGetValue(point.Tag.Id, out pending);
            if (pending is not null && ValuesEquivalent(point.Tag.DataType, pending.ExpectedValue, sample.Value))
                pending.Completion.TrySetResult(sample);
        }
    }

    private async Task MarkWriteUncertainAsync(EspHomePoint point, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        var sample = new TagValue(
            point.Tag.Id,
            previous?.Value,
            DateTimeOffset.UtcNow,
            TagQuality.Uncertain,
            DriverId)
        {
            SourceTimestamp = previous?.SourceTimestamp
        };
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updates);
    }

    private async Task MarkUnavailableAsync(TagQuality quality, CancellationToken cancellationToken)
    {
        foreach (var point in _points)
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
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _updates);
        }
    }

    private async Task MarkMissingEntityTagsAsync(
        IReadOnlyCollection<Guid> missingTagIds,
        CancellationToken cancellationToken)
    {
        foreach (var tagId in missingTagIds)
        {
            if (!_pointsById.TryGetValue(tagId, out var point)) continue;
            _cache.TryGet(tagId, out var previous);
            var sample = new TagValue(
                tagId,
                previous?.Value,
                DateTimeOffset.UtcNow,
                TagQuality.BadDevice,
                DriverId)
            {
                SourceTimestamp = previous?.SourceTimestamp
            };
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _updates);
        }
    }

    private async Task EnterFatalStateAsync(Exception ex, CancellationToken cancellationToken)
    {
        RecordFailure(ex, null);
        FailPendingWrites(ex);
        await SafeDisconnectAsync().ConfigureAwait(false);
        await MarkUnavailableAsync(TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
        Transition(CommunicationDriverOperationalState.Faulted);
        Status = new DriverStatus(
            DriverId,
            Name,
            DriverState.Faulted,
            DateTimeOffset.UtcNow,
            Sanitize(ex.Message),
            Interlocked.Read(ref _updates));
    }

    private async Task DelayReconnectAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var maxJitterMs = Math.Max(1, (int)Math.Min(delay.TotalMilliseconds / 4d, 1000d));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, maxJitterMs + 1));
        await Task.Delay(delay + jitter, cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan NextReconnectDelay(TimeSpan current)
    {
        var nextMs = Math.Min(
            current.TotalMilliseconds * 2d,
            _settings.EffectiveReconnectMaximumDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(nextMs);
    }

    private async Task SafeDisconnectAsync()
    {
        try
        {
            await _client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The transport is already considered unavailable. A close failure
            // must not replace the original protocol/connection diagnostic.
        }
    }

    private void FailPendingWrites(Exception error)
    {
        lock (_gate)
        {
            foreach (var pending in _pendingWrites.Values)
                pending.Completion.TrySetException(error);
            _pendingWrites.Clear();
        }
    }

    private void RecordSuccess(TimeSpan? duration)
    {
        lock (_gate)
        {
            _lastSuccess = DateTimeOffset.UtcNow;
            _lastError = null;
            if (duration.HasValue)
                RecordDurationNoLock(duration.Value);
        }
        Interlocked.Increment(ref _successful);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }

    private void RecordFailure(Exception error, TimeSpan? duration)
    {
        lock (_gate)
        {
            _lastFailure = DateTimeOffset.UtcNow;
            _lastError = Sanitize(error.Message);
            if (duration.HasValue)
                RecordDurationNoLock(duration.Value);
        }
        Interlocked.Increment(ref _failed);
        Interlocked.Increment(ref _consecutiveFailures);
    }

    private void RecordDurationNoLock(TimeSpan duration)
    {
        _lastOperationDuration = duration;
        var samples = ++_operationDurationSamples;
        _averageOperationMilliseconds +=
            (duration.TotalMilliseconds - _averageOperationMilliseconds) / samples;
    }

    private void Transition(CommunicationDriverOperationalState state)
    {
        lock (_gate)
        {
            if (_communicationState == state) return;
            _communicationState = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private string? CurrentStatusMessage()
    {
        lock (_gate)
        {
            if (_communicationState == CommunicationDriverOperationalState.Reconnecting)
                return _settings.DeepSleepPolicy == EspHomeDeepSleepPolicy.Expected
                    ? "ESPHome node is offline under expected deep-sleep policy; reconnect remains active."
                    : _lastError;
            return _lastError;
        }
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0; var comm = 0; var uncertain = 0; var bad = 0; var config = 0;
        var device = 0; var stale = 0; var disabled = 0; var noSample = 0;
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
                case TagQuality.BadCommunication: comm++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.BadConfiguration: config++; break;
                case TagQuality.BadDevice: device++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
                default: bad++; break;
            }
        }

        return new CommunicationTagQualitySummary(
            good, comm, uncertain, bad, config, device, stale, disabled, noSample);
    }

    private static object? ConvertStateValue(TagDataType type, object? value) => type switch
    {
        TagDataType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
        TagDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
        TagDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        TagDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        TagDataType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        TagDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        TagDataType.String or TagDataType.Enum => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => value
    };

    private static object? ConvertWriteValue(TagDataType type, object? value) =>
        ConvertStateValue(type, value);

    private static bool ValuesEquivalent(TagDataType type, object? expected, object? observed)
    {
        if (expected is null || observed is null) return expected is null && observed is null;
        return type switch
        {
            TagDataType.Float or TagDataType.Double =>
                Math.Abs(Convert.ToDouble(expected, CultureInfo.InvariantCulture) -
                         Convert.ToDouble(observed, CultureInfo.InvariantCulture)) <= 0.001d,
            TagDataType.Boolean =>
                Convert.ToBoolean(expected, CultureInfo.InvariantCulture) ==
                Convert.ToBoolean(observed, CultureInfo.InvariantCulture),
            _ => string.Equals(
                Convert.ToString(expected, CultureInfo.InvariantCulture),
                Convert.ToString(observed, CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
        };
    }

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
