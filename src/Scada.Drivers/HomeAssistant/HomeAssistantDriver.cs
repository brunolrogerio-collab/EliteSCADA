using System.Globalization;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.HomeAssistant;

public sealed class HomeAssistantResolvedCredential : IDisposable
{
    private byte[] _token;
    public HomeAssistantResolvedCredential(ReadOnlyMemory<byte> token) => _token = token.ToArray();
    public ReadOnlyMemory<byte> Token => _token;

    public void Dispose()
    {
        if (_token.Length > 0)
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(_token);
        _token = Array.Empty<byte>();
    }
}

public delegate ValueTask<HomeAssistantResolvedCredential> HomeAssistantCredentialResolver(
    CancellationToken cancellationToken);

public enum HomeAssistantWriteMode
{
    None,
    SwitchState,
    LightState,
    LightBrightnessPercent,
    LightRgb,
    CoverPosition
}

public sealed record HomeAssistantPoint(
    TagDefinition Tag,
    string EntityId,
    string Field,
    HomeAssistantWriteMode WriteMode = HomeAssistantWriteMode.None)
{
    public bool CanWrite => WriteMode != HomeAssistantWriteMode.None && !Tag.ReadOnly;
}

public sealed class HomeAssistantDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource
{
    private readonly HomeAssistantConnectionSettings _settings;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyCollection<HomeAssistantPoint> _points;
    private readonly IReadOnlyDictionary<Guid, HomeAssistantPoint> _pointsById;
    private readonly Func<HomeAssistantConnectionSettings, IHomeAssistantClient> _clientFactory;
    private readonly HomeAssistantCredentialResolver _credentials;
    private readonly Func<bool> _effectAuthority;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly object _gate = new();

    private IHomeAssistantClient? _client;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private bool _disposed;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _lastFailure;
    private DateTimeOffset? _lastReconciliation;
    private DateTimeOffset? _lastEvent;
    private DateTimeOffset? _lastRead;
    private DateTimeOffset? _lastWrite;
    private string? _lastError;
    private string? _haVersion;

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

    public HomeAssistantDriver(
        string driverId,
        string name,
        HomeAssistantConnectionSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IReadOnlyCollection<HomeAssistantPoint> points,
        Func<HomeAssistantConnectionSettings, IHomeAssistantClient> clientFactory,
        HomeAssistantCredentialResolver credentials,
        Func<bool>? effectAuthority = null)
    {
        DriverId = driverId;
        Name = name;
        _settings = settings;
        _cache = cache;
        _registry = registry;
        _points = points;
        _pointsById = points.ToDictionary(x => x.Tag.Id);
        _clientFactory = clientFactory;
        _credentials = credentials;
        _effectAuthority = effectAuthority ?? (() => true);
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
            await ConnectFreshSessionAsync(reconnect: false, _runCts.Token).ConfigureAwait(false);
            _runTask = RunEventsAsync(_runCts.Token);
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            Transition(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(
                DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow,
                SafeFailure(ex), Interlocked.Read(ref _updates));
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

        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _client;
            _client = null;
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
        }

        _runTask = null;
        _runCts = null;
        cts.Dispose();
        Transition(CommunicationDriverOperationalState.Stopped);
        Status = new DriverStatus(
            DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow,
            UpdatesPublished: Interlocked.Read(ref _updates));
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsById.ContainsKey(tagId))
            throw new KeyNotFoundException($"Home Assistant TAG '{tagId}' is not owned by this driver.");

        Interlocked.Increment(ref _reads);
        lock (_gate) _lastRead = DateTimeOffset.UtcNow;
        return ValueTask.FromResult(_cache.TryGet(tagId, out var value) ? value : null);
    }

    public async ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_pointsById.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Home Assistant TAG '{tagId}' is not owned by this driver.");
        if (!point.CanWrite)
            throw new InvalidOperationException($"Home Assistant TAG '{point.Tag.Path}' is read-only.");
        if (!_effectAuthority())
            throw new InvalidOperationException("Home Assistant external write authority is not owned by this runtime instance.");

        var invocation = BuildWrite(point, value);
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _client ?? throw new IOException("Home Assistant session is not connected.");
            Interlocked.Increment(ref _writes);
            Interlocked.Increment(ref _requests);
            try
            {
                await client.CallServiceAsync(
                    invocation.Domain,
                    invocation.Service,
                    point.EntityId,
                    invocation.ServiceData,
                    cancellationToken).ConfigureAwait(false);
                RecordSuccess();

                // Service acceptance is not process truth. Never publish the
                // requested value directly. Re-read authoritative HA state.
                Interlocked.Increment(ref _requests);
                var states = await client.GetStatesAsync(cancellationToken).ConfigureAwait(false);
                RecordSuccess();
                await PublishStatesAsync(states, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    _lastWrite = DateTimeOffset.UtcNow;
                    _lastReconciliation = DateTimeOffset.UtcNow;
                }
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
        finally
        {
            _sessionGate.Release();
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        DateTimeOffset? lastSuccess;
        DateTimeOffset? lastFailure;
        DateTimeOffset? lastReconciliation;
        DateTimeOffset? lastEvent;
        DateTimeOffset? lastRead;
        DateTimeOffset? lastWrite;
        string? lastError;
        string? haVersion;
        CommunicationDriverOperationalState state;
        DateTimeOffset stateChanged;
        lock (_gate)
        {
            lastSuccess = _lastSuccess;
            lastFailure = _lastFailure;
            lastReconciliation = _lastReconciliation;
            lastEvent = _lastEvent;
            lastRead = _lastRead;
            lastWrite = _lastWrite;
            lastError = _lastError;
            haVersion = _haVersion;
            state = _communicationState;
            stateChanged = _stateChangedAt;
        }

        return new CommunicationDriverDiagnosticSnapshot(
            DriverId,
            Name,
            HomeAssistantContract.DriverType,
            DriverId,
            $"{(_settings.UseTls ? "https" : "http")}://{_settings.Host}:{_settings.Port}{NormalizeBasePath(_settings.BasePath)}",
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
                Cycles: Interlocked.Read(ref _cycles),
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
                ["haVersion"] = haVersion ?? string.Empty,
                ["auth"] = "protected-reference",
                ["eventSubscription"] = "state_changed",
                ["eventQueueCapacity"] = HomeAssistantContract.StateEventQueueCapacity.ToString(CultureInfo.InvariantCulture),
                ["eventBackpressure"] = "bounded-wait-no-drop",
                ["lastFullReconciliation"] = lastReconciliation?.ToString("O") ?? string.Empty,
                ["lastStateChangedEvent"] = lastEvent?.ToString("O") ?? string.Empty,
                ["lastSuccessfulRead"] = lastRead?.ToString("O") ?? string.Empty,
                ["lastSuccessfulWriteReadback"] = lastWrite?.ToString("O") ?? string.Empty,
                ["writeTruth"] = "authoritative-state-readback"
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _sessionGate.Dispose();
    }

    private async Task RunEventsAsync(CancellationToken cancellationToken)
    {
        var delay = _settings.EffectiveReconnectMinimumDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = GetClient();
                var change = await client.ReceiveStateChangedAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _cycles);
                lock (_gate) _lastEvent = DateTimeOffset.UtcNow;

                if (change.NewState is not null)
                    await PublishStateAsync(change.NewState, cancellationToken).ConfigureAwait(false);
                else
                    await MarkEntityUnavailableAsync(change.EntityId, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);

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
                Status = new DriverStatus(
                    DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow,
                    SafeFailure(ex), Interlocked.Read(ref _updates));
                Interlocked.Increment(ref _disconnections);
                await MarkAllUnavailableAsync(cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var jitterMs = Random.Shared.Next(
                        0,
                        Math.Max(2, (int)Math.Min(delay.TotalMilliseconds / 4, 1000)));
                    await Task.Delay(delay + TimeSpan.FromMilliseconds(jitterMs), cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await ConnectFreshSessionAsync(reconnect: true, cancellationToken).ConfigureAwait(false);
                        delay = _settings.EffectiveReconnectMinimumDelay;
                        break;
                    }
                    catch (Exception reconnectError) when (reconnectError is not OperationCanceledException)
                    {
                        RecordFailure(reconnectError);
                        var nextMs = Math.Min(
                            delay.TotalMilliseconds * 2,
                            _settings.EffectiveReconnectMaximumDelay.TotalMilliseconds);
                        delay = TimeSpan.FromMilliseconds(nextMs);
                    }
                }
            }
        }
    }

    private async Task ConnectFreshSessionAsync(bool reconnect, CancellationToken cancellationToken)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IHomeAssistantClient? candidate = null;
        try
        {
            using var credential = await _credentials(cancellationToken).ConfigureAwait(false);
            candidate = _clientFactory(_settings);
            Interlocked.Increment(ref _requests);
            await candidate.ConnectAndAuthenticateAsync(credential.Token, cancellationToken).ConfigureAwait(false);
            RecordSuccess();

            Interlocked.Increment(ref _requests);
            var states = await candidate.GetStatesAsync(cancellationToken).ConfigureAwait(false);
            RecordSuccess();
            await PublishStatesAsync(states, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _requests);
            _ = await candidate.SubscribeStateChangedAsync(cancellationToken).ConfigureAwait(false);
            RecordSuccess();

            var previous = _client;
            _client = candidate;
            candidate = null;
            if (previous is not null)
                await previous.DisposeAsync().ConfigureAwait(false);

            Interlocked.Increment(ref _connections);
            if (reconnect) Interlocked.Increment(ref _reconnects);
            lock (_gate)
            {
                _haVersion = _client.HomeAssistantVersion;
                _lastReconciliation = DateTimeOffset.UtcNow;
            }
            Transition(CommunicationDriverOperationalState.Healthy);
            Status = new DriverStatus(
                DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow,
                UpdatesPublished: Interlocked.Read(ref _updates));
        }
        finally
        {
            if (candidate is not null)
                await candidate.DisposeAsync().ConfigureAwait(false);
            _sessionGate.Release();
        }
    }

    private IHomeAssistantClient GetClient()
    {
        lock (_gate)
            return _client ?? throw new IOException("Home Assistant session is not connected.");
    }

    private async Task PublishStatesAsync(
        IReadOnlyCollection<HomeAssistantState> states,
        CancellationToken cancellationToken)
    {
        var byId = states
            .GroupBy(x => x.EntityId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        foreach (var point in _points)
        {
            if (byId.TryGetValue(point.EntityId, out var state))
                await PublishPointAsync(point, state, cancellationToken).ConfigureAwait(false);
            else
                await PublishUnavailablePointAsync(point, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishStateAsync(HomeAssistantState state, CancellationToken cancellationToken)
    {
        foreach (var point in _points.Where(x => string.Equals(x.EntityId, state.EntityId, StringComparison.Ordinal)))
            await PublishPointAsync(point, state, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishPointAsync(
        HomeAssistantPoint point,
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        if (string.Equals(state.State, "unavailable", StringComparison.OrdinalIgnoreCase))
        {
            await PublishUnavailablePointAsync(point, TagQuality.BadDevice, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(state.State, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            await PublishUnavailablePointAsync(point, TagQuality.Uncertain, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryConvertPointValue(point, state, out var value))
            return;

        var sample = new TagValue(
            point.Tag.Id,
            value,
            DateTimeOffset.UtcNow,
            TagQuality.Good,
            DriverId)
        {
            SourceTimestamp = state.LastUpdated ?? state.LastChanged
        };
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updates);
    }

    private async Task MarkEntityUnavailableAsync(
        string entityId,
        TagQuality quality,
        CancellationToken cancellationToken)
    {
        foreach (var point in _points.Where(x => string.Equals(x.EntityId, entityId, StringComparison.Ordinal)))
            await PublishUnavailablePointAsync(point, quality, cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkAllUnavailableAsync(CancellationToken cancellationToken)
    {
        foreach (var point in _points)
            await PublishUnavailablePointAsync(point, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishUnavailablePointAsync(
        HomeAssistantPoint point,
        TagQuality quality,
        CancellationToken cancellationToken)
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

    private static bool TryConvertPointValue(
        HomeAssistantPoint point,
        HomeAssistantState state,
        out object? value)
    {
        if (string.Equals(point.Field, "state", StringComparison.Ordinal))
            return TryConvertScalar(point.Tag.DataType, state.State, out value);

        if (state.Attributes.ValueKind != JsonValueKind.Object ||
            !state.Attributes.TryGetProperty(point.Field, out var attribute) ||
            attribute.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            value = null;
            return false;
        }

        if (string.Equals(point.Field, "brightness", StringComparison.Ordinal) &&
            attribute.TryGetDouble(out var brightness))
        {
            value = Math.Clamp(brightness / 255d * 100d, 0d, 100d);
            return true;
        }

        if (string.Equals(point.Field, "rgb_color", StringComparison.Ordinal) &&
            attribute.ValueKind == JsonValueKind.Array)
        {
            var values = attribute.EnumerateArray()
                .Select(x => x.TryGetInt32(out var part) ? part : -1)
                .ToArray();
            if (values.Length == 3 && values.All(x => x is >= 0 and <= 255))
            {
                value = string.Join(",", values);
                return true;
            }
            value = null;
            return false;
        }

        if (point.Tag.DataType == TagDataType.Double && attribute.TryGetDouble(out var numeric))
        {
            value = numeric;
            return true;
        }

        if (point.Tag.DataType == TagDataType.Boolean &&
            attribute.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = attribute.GetBoolean();
            return true;
        }

        if (point.Tag.DataType is TagDataType.String or TagDataType.Enum)
        {
            value = attribute.ValueKind == JsonValueKind.String
                ? attribute.GetString()
                : attribute.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryConvertScalar(TagDataType type, string raw, out object? value)
    {
        switch (type)
        {
            case TagDataType.Boolean:
                if (string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                {
                    value = true;
                    return true;
                }
                if (string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
                {
                    value = false;
                    return true;
                }
                break;
            case TagDataType.Double:
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    value = d;
                    return true;
                }
                break;
            case TagDataType.Float:
                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    value = f;
                    return true;
                }
                break;
            case TagDataType.Int16:
                if (short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i16))
                {
                    value = i16;
                    return true;
                }
                break;
            case TagDataType.Int32:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32))
                {
                    value = i32;
                    return true;
                }
                break;
            case TagDataType.Int64:
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i64))
                {
                    value = i64;
                    return true;
                }
                break;
            case TagDataType.String:
            case TagDataType.Enum:
                value = raw;
                return true;
        }

        value = null;
        return false;
    }

    private static HomeAssistantServiceInvocation BuildWrite(HomeAssistantPoint point, object? value)
    {
        return point.WriteMode switch
        {
            HomeAssistantWriteMode.SwitchState =>
                BooleanAction("switch", value),
            HomeAssistantWriteMode.LightState =>
                BooleanAction("light", value),
            HomeAssistantWriteMode.LightBrightnessPercent =>
                new("light", "turn_on", new Dictionary<string, object?>
                {
                    ["brightness_pct"] = ClampDouble(value, 0, 100)
                }),
            HomeAssistantWriteMode.LightRgb =>
                new("light", "turn_on", new Dictionary<string, object?>
                {
                    ["rgb_color"] = ParseRgb(value)
                }),
            HomeAssistantWriteMode.CoverPosition =>
                new("cover", "set_cover_position", new Dictionary<string, object?>
                {
                    ["position"] = ClampDouble(value, 0, 100)
                }),
            _ => throw new InvalidOperationException(
                $"Home Assistant TAG '{point.Tag.Path}' has no bounded write profile.")
        };
    }

    private static HomeAssistantServiceInvocation BooleanAction(string domain, object? value) =>
        new(domain, Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "turn_on" : "turn_off", null);

    private static double ClampDouble(object? value, double minimum, double maximum)
    {
        var converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (double.IsNaN(converted) || double.IsInfinity(converted) || converted < minimum || converted > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {minimum} and {maximum}.");
        return converted;
    }

    private static int[] ParseRgb(object? value)
    {
        if (value is int[] parts && parts.Length == 3 && parts.All(x => x is >= 0 and <= 255))
            return parts;

        var raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var parsed = raw.Split(',', StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var part) ? part : -1)
            .ToArray();
        if (parsed.Length != 3 || parsed.Any(x => x is < 0 or > 255))
            throw new ArgumentException("RGB value must be 'r,g,b' with components from 0 through 255.", nameof(value));
        return parsed;
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
            _lastError = SafeFailure(ex);
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

    private static string SafeFailure(Exception ex) =>
        ex switch
        {
            HomeAssistantAuthenticationException => "Home Assistant authentication failed.",
            TimeoutException => "Home Assistant operation timed out.",
            _ => "Home Assistant communication failed."
        };

    private static string NormalizeBasePath(string basePath) =>
        string.IsNullOrWhiteSpace(basePath) ? string.Empty : $"/{basePath.Trim('/')}";

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record HomeAssistantServiceInvocation(
        string Domain,
        string Service,
        IReadOnlyDictionary<string, object?>? ServiceData);
}