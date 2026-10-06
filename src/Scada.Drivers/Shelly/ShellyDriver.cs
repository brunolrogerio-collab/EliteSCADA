using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Shelly;

public sealed class ShellyResolvedCredential : IDisposable
{
    private byte[] _password;

    public ShellyResolvedCredential(ReadOnlyMemory<byte> password) => _password = password.ToArray();
    public ReadOnlyMemory<byte> Password => _password;

    public void Dispose()
    {
        if (_password.Length > 0)
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(_password);
        _password = Array.Empty<byte>();
    }
}

public delegate ValueTask<ShellyResolvedCredential> ShellyCredentialResolver(CancellationToken cancellationToken);

public sealed class ShellyDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource
{
    private readonly ShellyConnectionSettings _settings;
    private readonly ICurrentTagCache _cache;
    private readonly IReadOnlyCollection<ShellyPoint> _points;
    private readonly IReadOnlyDictionary<Guid, ShellyPoint> _pointsById;
    private readonly IShellyRpcClient _client;
    private readonly ShellyCredentialResolver _credentials;
    private readonly Dictionary<string, JsonElement> _state = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private ShellyDeviceInfo? _device;
    private int _componentCount;
    private bool _legacyAuthentication;
    private bool _disposed;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _lastFailure;
    private DateTimeOffset? _lastFullReconciliation;
    private DateTimeOffset? _lastNotification;
    private DateTimeOffset? _lastRead;
    private DateTimeOffset? _lastWrite;
    private string? _lastError;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
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
    private long _notifications;
    private long _transientEvents;

    public ShellyDriver(
        string driverId,
        string name,
        ShellyConnectionSettings settings,
        ICurrentTagCache cache,
        IReadOnlyCollection<ShellyPoint> points,
        IShellyRpcClient client,
        ShellyCredentialResolver credentials)
    {
        DriverId = driverId;
        Name = name;
        _settings = settings;
        _cache = cache;
        _points = points;
        _pointsById = points.ToDictionary(x => x.Tag.Id);
        _client = client;
        _credentials = credentials;
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

        Transition(CommunicationDriverOperationalState.Starting);
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await BootstrapAsync(_runCts.Token).ConfigureAwait(false);
            _runTask = RunNotificationsAsync(_runCts.Token);
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            Transition(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, Sanitize(ex.Message), Interlocked.Read(ref _updates));
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
        await _client.DisconnectNotificationsAsync(cancellationToken).ConfigureAwait(false);
        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _runTask = null;
        _runCts = null;
        cts.Dispose();
        Transition(CommunicationDriverOperationalState.Stopped);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsById.ContainsKey(tagId))
            throw new KeyNotFoundException($"Shelly TAG '{tagId}' is not owned by this driver.");
        Interlocked.Increment(ref _reads);
        lock (_gate) _lastRead = DateTimeOffset.UtcNow;
        return ValueTask.FromResult(_cache.TryGet(tagId, out var value) ? value : null);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_pointsById.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Shelly TAG '{tagId}' is not owned by this driver.");
        if (!point.CanWrite)
            throw new InvalidOperationException($"Shelly TAG '{point.Tag.Path}' is read-only.");

        var componentId = ParseComponentId(point.ComponentKey);
        var parameters = new Dictionary<string, object?>
        {
            ["id"] = componentId,
            [point.WriteParameter!] = ConvertWriteValue(point.Tag.DataType, value)
        };

        using var credential = await _credentials(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            await _client.CallHttpAsync(
                point.WriteMethod!,
                parameters,
                credential.Password,
                _legacyAuthentication,
                cancellationToken).ConfigureAwait(false);
            RecordSuccess();
            lock (_gate) _lastWrite = DateTimeOffset.UtcNow;

            // RPC acceptance is not process truth. A successful write is only
            // committed to the cache after a fresh device status reconciliation.
            await FullReconcileAsync(credential.Password, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Interlocked.Increment(ref _timeouts);
            throw;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        DateTimeOffset? lastSuccess;
        DateTimeOffset? lastFailure;
        DateTimeOffset? lastFull;
        DateTimeOffset? lastNotification;
        DateTimeOffset? lastRead;
        DateTimeOffset? lastWrite;
        string? lastError;
        CommunicationDriverOperationalState state;
        DateTimeOffset stateChanged;
        ShellyDeviceInfo? device;
        lock (_gate)
        {
            lastSuccess = _lastSuccess;
            lastFailure = _lastFailure;
            lastFull = _lastFullReconciliation;
            lastNotification = _lastNotification;
            lastRead = _lastRead;
            lastWrite = _lastWrite;
            lastError = _lastError;
            state = _communicationState;
            stateChanged = _stateChangedAt;
            device = _device;
        }

        return new CommunicationDriverDiagnosticSnapshot(
            DriverId,
            Name,
            ShellyRpcContract.DriverType,
            DriverId,
            $"{(_settings.UseTls ? "https" : "http")}://{_settings.Host}:{_settings.Port}",
            state,
            stateChanged,
            DateTimeOffset.UtcNow,
            lastSuccess,
            lastFailure,
            lastError,
            lastSuccess.HasValue ? DateTimeOffset.UtcNow - lastSuccess.Value : null,
            null,
            null,
            null,
            null,
            RecentFailureRate: Interlocked.Read(ref _failed) == 0 ? 0 : 1,
            AssociatedTagCount: _points.Count,
            TagQuality: BuildQualitySummary(),
            Counters: new CommunicationDriverCounters(
                Cycles: Interlocked.Read(ref _notifications),
                Requests: Interlocked.Read(ref _requests),
                SuccessfulOperations: Interlocked.Read(ref _successful),
                FailedOperations: Interlocked.Read(ref _failed),
                ConsecutiveFailures: Interlocked.Read(ref _consecutiveFailures),
                Timeouts: Interlocked.Read(ref _timeouts),
                Connections: Interlocked.Read(ref _connections),
                Disconnections: Interlocked.Read(ref _disconnections),
                Reconnects: Interlocked.Read(ref _reconnects),
                ReadOperations: Interlocked.Read(ref _reads),
                WriteOperations: Interlocked.Read(ref _writes),
                UpdatesPublished: Interlocked.Read(ref _updates)),
            ProtocolDetails: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stableDeviceIdentity"] = device?.StableDeviceIdentity ?? string.Empty,
                ["model"] = device?.Model ?? string.Empty,
                ["firmware"] = device?.Firmware ?? string.Empty,
                ["auth"] = "protected-reference-or-disabled",
                ["webSocketConnected"] = _client.WebSocketConnected.ToString(CultureInfo.InvariantCulture),
                ["componentCount"] = _componentCount.ToString(CultureInfo.InvariantCulture),
                ["lastFullReconciliation"] = lastFull?.ToString("O") ?? string.Empty,
                ["lastNotification"] = lastNotification?.ToString("O") ?? string.Empty,
                ["lastSuccessfulRead"] = lastRead?.ToString("O") ?? string.Empty,
                ["lastSuccessfulWriteReadback"] = lastWrite?.ToString("O") ?? string.Empty,
                ["transientEventsObserved"] = Interlocked.Read(ref _transientEvents).ToString(CultureInfo.InvariantCulture)
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        using var credential = await _credentials(cancellationToken).ConfigureAwait(false);

        // GetDeviceInfo is intentionally available without authentication and gives
        // us stable physical identity and firmware before protected calls.
        var deviceJson = await CallAsync("Shelly.GetDeviceInfo", null, ReadOnlyMemory<byte>.Empty, legacy: false, cancellationToken).ConfigureAwait(false);
        var device = ShellyStateMapper.ParseDeviceInfo(deviceJson);
        _legacyAuthentication = IsLegacyFirmware(device.Firmware);
        lock (_gate) _device = device;

        var components = await CallAsync(
            "Shelly.GetComponents",
            new { offset = 0 },
            credential.Password,
            _legacyAuthentication,
            cancellationToken).ConfigureAwait(false);
        _componentCount = components.TryGetProperty("total", out var total) && total.TryGetInt32(out var count)
            ? count
            : components.TryGetProperty("components", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.GetArrayLength()
                : 0;

        await FullReconcileAsync(credential.Password, cancellationToken).ConfigureAwait(false);
        await _client.ConnectNotificationsAsync(credential.Password, _legacyAuthentication, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _connections);
        Transition(CommunicationDriverOperationalState.Healthy);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
    }

    private async Task RunNotificationsAsync(CancellationToken cancellationToken)
    {
        var delay = _settings.EffectiveReconnectMinimumDelay;
        var connectedOnce = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var notification = await _client.ReceiveNotificationAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _notifications);
                lock (_gate) _lastNotification = DateTimeOffset.UtcNow;

                if (notification.Method == "NotifyStatus")
                {
                    lock (_gate) ShellyStateMapper.MergeStatus(_state, notification.Parameters);
                    await PublishStateAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (notification.Method == "NotifyEvent")
                {
                    // Transient action events are deliberately diagnostic-only in
                    // this lane. No persistent TAG is fabricated from click/push.
                    Interlocked.Increment(ref _transientEvents);
                }

                RecordSuccess();
                delay = _settings.EffectiveReconnectMinimumDelay;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException) Interlocked.Increment(ref _timeouts);
                RecordFailure(ex);
                Transition(CommunicationDriverOperationalState.Reconnecting);
                Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, Sanitize(ex.Message), Interlocked.Read(ref _updates));
                await MarkUnavailableAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _disconnections);
                await _client.DisconnectNotificationsAsync(CancellationToken.None).ConfigureAwait(false);

                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, Math.Max(2, (int)Math.Min(delay.TotalMilliseconds / 4, 1000))));
                await Task.Delay(delay + jitter, cancellationToken).ConfigureAwait(false);

                try
                {
                    using var credential = await _credentials(cancellationToken).ConfigureAwait(false);
                    await _client.ConnectNotificationsAsync(credential.Password, _legacyAuthentication, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _connections);
                    if (connectedOnce) Interlocked.Increment(ref _reconnects);
                    connectedOnce = true;

                    // Full reconciliation is mandatory after reconnect and before
                    // the Runtime returns to Healthy/process-truth status.
                    await FullReconcileAsync(credential.Password, cancellationToken).ConfigureAwait(false);
                    Transition(CommunicationDriverOperationalState.Healthy);
                    Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updates));
                    delay = _settings.EffectiveReconnectMinimumDelay;
                }
                catch (Exception reconnectError) when (reconnectError is not OperationCanceledException)
                {
                    RecordFailure(reconnectError);
                    var nextMs = Math.Min(delay.TotalMilliseconds * 2, _settings.EffectiveReconnectMaximumDelay.TotalMilliseconds);
                    delay = TimeSpan.FromMilliseconds(nextMs);
                }
            }
        }
    }

    private async ValueTask<JsonElement> CallAsync(
        string method,
        object? parameters,
        ReadOnlyMemory<byte> password,
        bool legacy,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await _client.CallHttpAsync(method, parameters, password, legacy, cancellationToken).ConfigureAwait(false);
            _ = Stopwatch.GetElapsedTime(started);
            RecordSuccess();
            return result;
        }
        catch (TimeoutException)
        {
            Interlocked.Increment(ref _timeouts);
            throw;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    private async Task FullReconcileAsync(ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        var full = await CallAsync("Shelly.GetStatus", null, password, _legacyAuthentication, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _state.Clear();
            ShellyStateMapper.MergeStatus(_state, full);
            _lastFullReconciliation = DateTimeOffset.UtcNow;
        }
        await PublishStateAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishStateAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, JsonElement> snapshot;
        lock (_gate) snapshot = _state.ToDictionary(x => x.Key, x => x.Value.Clone(), StringComparer.Ordinal);

        foreach (var point in _points)
        {
            if (!ShellyStateMapper.TryReadPoint(snapshot, point, out var value))
                continue;
            var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, TagQuality.Good, DriverId);
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _updates);
        }
    }

    private async Task MarkUnavailableAsync(CancellationToken cancellationToken)
    {
        foreach (var point in _points)
        {
            _cache.TryGet(point.Tag.Id, out var previous);
            var sample = new TagValue(
                point.Tag.Id,
                previous?.Value,
                DateTimeOffset.UtcNow,
                TagQuality.BadCommunication,
                DriverId)
            {
                SourceTimestamp = previous?.SourceTimestamp
            };
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _updates);
        }
    }

    private static int ParseComponentId(string componentKey)
    {
        var separator = componentKey.IndexOf(':');
        if (separator < 0) return 0;
        return int.TryParse(componentKey[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException($"Shelly component key '{componentKey}' has no numeric id.");
    }

    private static object? ConvertWriteValue(TagDataType type, object? value) => type switch
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

    public static bool IsLegacyFirmware(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        var first = version.Split('-', '+')[0];
        return Version.TryParse(first, out var parsed) && parsed.Major < 2;
    }

    private void RecordSuccess()
    {
        lock (_gate)
        {
            _lastSuccess = DateTimeOffset.UtcNow;
            _lastError = null;
        }
        Interlocked.Increment(ref _successful);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }

    private void RecordFailure(Exception ex)
    {
        lock (_gate)
        {
            _lastFailure = DateTimeOffset.UtcNow;
            _lastError = Sanitize(ex.Message);
        }
        Interlocked.Increment(ref _failed);
        Interlocked.Increment(ref _consecutiveFailures);
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

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0; var comm = 0; var uncertain = 0; var bad = 0; var config = 0;
        var device = 0; var stale = 0; var disabled = 0; var noSample = 0;
        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var sample) || sample is null) { noSample++; continue; }
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
        return new CommunicationTagQualitySummary(good, comm, uncertain, bad, config, device, stale, disabled, noSample);
    }

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
