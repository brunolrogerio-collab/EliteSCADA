using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.AllenBradley;

public sealed class AllenBradleyLogixDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private const int RecentOutcomeWindow = 100;

    private readonly AllenBradleyLogixOptions _options;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<LogixTagBinding> _bindings;
    private readonly IReadOnlyDictionary<Guid, LogixTagBinding> _bindingsByTagId;
    private readonly IReadOnlyList<SymbolGroup> _symbolGroups;
    private readonly ILogixProtocolClient _client;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private CommunicationDriverOperationalState _communicationState;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private DateTimeOffset? _lastSuccessfulPollAt;
    private DateTimeOffset? _lastFailedPollAt;
    private string? _lastError;
    private long _pollCycles;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _readOperations;
    private long _writeOperations;
    private long _updatesPublished;
    private long _failedPollCycles;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private long _operationDurationSamples;
    private long _lastScanDurationTicks;
    private TimeSpan _reconnectDelay;

    public AllenBradleyLogixDriver(
        string driverId,
        string name,
        AllenBradleyLogixOptions options,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<LogixTagBinding> bindings,
        ILogixProtocolClientFactory? clientFactory = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(bindings);

        options.Validate();
        if (options.SecurityMode == LogixSecurityMode.CipSecurityRequired)
            throw new NotSupportedException("This Allen-Bradley first cut does not implement CIP Security and will not silently downgrade to unsecured EtherNet/IP.");

        DriverId = driverId.Trim();
        Name = name.Trim();
        _options = options;
        _cache = cache;
        _registry = registry;
        _bindings = bindings.ToArray();
        if (_bindings.Count == 0) throw new ArgumentException("At least one Logix TAG binding is required.", nameof(bindings));

        foreach (var binding in _bindings) binding.Validate();
        if (_bindings.Select(static x => x.Tag.Id).Distinct().Count() != _bindings.Count)
            throw new ArgumentException("Each Logix binding must reference a unique canonical TAG ID.", nameof(bindings));

        foreach (var group in _bindings.GroupBy(static x => x.Reference.StableIdentity, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Select(static x => x.Reference.NativeType).Distinct().Count() != 1)
                throw new ArgumentException($"Logix symbol '{group.Key}' is bound with conflicting native data types.", nameof(bindings));
        }

        _bindingsByTagId = _bindings.ToDictionary(static x => x.Tag.Id);
        _symbolGroups = _bindings
            .GroupBy(static x => x.Reference.StableIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new SymbolGroup(group.First().Reference, group.ToArray()))
            .ToArray();

        _client = (clientFactory ?? new LogixEtherNetIpClientFactory()).Create();
        _reconnectDelay = _options.EffectiveReconnectMinimum;
        var now = DateTimeOffset.UtcNow;
        _communicationState = CommunicationDriverOperationalState.Stopped;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, now);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _bindings.Select(static x => x.Tag).ToArray();
    public AllenBradleyLogixOptions Options => _options;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        foreach (var binding in _bindings) _registry.Register(binding.Tag);
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionCommunicationState(CommunicationDriverOperationalState.Starting);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _cts;
        var loop = _loop;
        if (cts is null && loop is null)
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync(cancellationToken);
            return;
        }

        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        TransitionCommunicationState(CommunicationDriverOperationalState.Stopping);

        if (cts is not null) await cts.CancelAsync();
        if (loop is not null)
        {
            try { await loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cts?.IsCancellationRequested == true) { }
        }

        try
        {
            if (_client.IsConnected) await _client.DisconnectAsync(cancellationToken);
        }
        finally
        {
            _loop = null;
            _cts = null;
            cts?.Dispose();
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_bindingsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"Allen-Bradley TAG '{tagId}' was not found in driver '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_bindingsByTagId.TryGetValue(tagId, out var binding))
            throw new KeyNotFoundException($"Allen-Bradley TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!binding.Writable)
            throw new InvalidOperationException($"Allen-Bradley TAG '{binding.Tag.Path}' is not writable.");

        await _writeGate.WaitAsync(cancellationToken);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            object canonicalValue;

            if (binding.AddressSelector is null)
            {
                var native = LogixValueCodec.ToNativeWriteValue(binding, value);
                await _client.WriteAsync(binding.Reference, native, cancellationToken);
                canonicalValue = value ?? throw new ArgumentNullException(nameof(value));
            }
            else
            {
                if (value is not bool bitValue)
                    throw new ArgumentException("Physical Logix bit writes require a Boolean canonical value.", nameof(value));

                var read = await _client.ReadManyAsync([binding.Reference], cancellationToken);
                if (read.Count != 1)
                    throw new InvalidDataException("Logix physical bit read returned an unexpected result count.");
                var result = read[0];
                if (!result.Succeeded || result.NativeValue is null)
                    throw new InvalidOperationException($"Cannot perform Logix bit read-modify-write for '{binding.Tag.Path}': {result.Message ?? result.Error.ToString()}.");

                var updatedNative = LogixValueCodec.ApplyPhysicalBit(
                    binding.Reference.NativeType,
                    result.NativeValue,
                    binding.AddressSelector.Index,
                    bitValue);
                await _client.WriteAsync(binding.Reference, updatedNative, cancellationToken);
                canonicalValue = bitValue;
            }

            RecordOperation(success: true, read: false, write: true, Stopwatch.GetElapsedTime(started), null);
            TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
            await PublishAsync(binding, canonicalValue, TagQuality.Good, cancellationToken);
        }
        catch (LogixCipException ex)
        {
            RecordOperation(success: false, read: false, write: true, Stopwatch.GetElapsedTime(started), ex.Message);
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
        catch (Exception ex) when (IsTransportException(ex))
        {
            RecordOperation(success: false, read: false, write: true, Stopwatch.GetElapsedTime(started), ex.Message);
            TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
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
        var transport = _client.GetDiagnostics();
        var quality = BuildQualitySummary();

        lock (_diagnosticsGate)
        {
            var averageDuration = _operationDurationSamples == 0
                ? (TimeSpan?)null
                : TimeSpan.FromTicks(_totalOperationDurationTicks / _operationDurationSamples);
            var failureRate = _recentFailures.Count == 0
                ? 0d
                : _recentFailures.Count(static x => x) / (double)_recentFailures.Count;
            var dataAge = _lastSuccessfulPollAt.HasValue ? capturedAt - _lastSuccessfulPollAt.Value : (TimeSpan?)null;
            var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["profile"] = _options.Profile.ToString(),
                ["route"] = _options.RouteDisplay,
                ["messagingMode"] = "unconnected-explicit",
                ["securityMode"] = _options.SecurityMode.ToString(),
                ["physicalSymbolCount"] = _symbolGroups.Count.ToString(CultureInfo.InvariantCulture),
                ["maxBatchSize"] = _options.MaxBatchSize.ToString(CultureInfo.InvariantCulture),
                ["failedPollCycles"] = _failedPollCycles.ToString(CultureInfo.InvariantCulture),
                ["connected"] = transport.Connected.ToString(CultureInfo.InvariantCulture)
            };

            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                "rockwell.logix.eip",
                _runtimeInstanceId,
                _options.Endpoint,
                _communicationState,
                _stateChangedAt,
                capturedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                dataAge,
                _options.EffectiveScanInterval,
                _operationDurationSamples == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                averageDuration,
                _pollCycles == 0 ? null : TimeSpan.FromTicks(_lastScanDurationTicks),
                failureRate,
                _bindings.Count,
                quality,
                new CommunicationDriverCounters(
                    _pollCycles,
                    transport.RequestAttempts,
                    _successfulOperations,
                    _failedOperations,
                    _consecutiveFailures,
                    transport.TimeoutCount,
                    transport.ConnectionCount,
                    transport.DisconnectionCount,
                    transport.ReconnectCount,
                    _readOperations,
                    _writeOperations,
                    Interlocked.Read(ref _updatesPublished)),
                protocolDetails);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await EnsureConnectedAsync(cancellationToken);
                    await PollOnceAsync(cancellationToken);
                    _reconnectDelay = _options.EffectiveReconnectMinimum;
                    await Task.Delay(_options.EffectiveScanInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (IsTransportException(ex))
                {
                    RecordFailureOnly(ex);
                    TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
                    Status = new DriverStatus(
                        DriverId,
                        Name,
                        DriverState.Running,
                        DateTimeOffset.UtcNow,
                        $"Allen-Bradley communication unavailable: {Sanitize(ex.Message)}",
                        Interlocked.Read(ref _updatesPublished));
                    await PublishAllCommunicationFailureAsync(cancellationToken);
                    await SafeDisconnectAsync(cancellationToken);
                    await Task.Delay(_reconnectDelay, cancellationToken);
                    _reconnectDelay = TimeSpan.FromMilliseconds(Math.Min(
                        _options.EffectiveReconnectMaximum.TotalMilliseconds,
                        Math.Max(_options.EffectiveReconnectMinimum.TotalMilliseconds, _reconnectDelay.TotalMilliseconds * 2d)));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            RecordFailureOnly(ex);
            TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Faulted,
                DateTimeOffset.UtcNow,
                Sanitize(ex.Message),
                Interlocked.Read(ref _updatesPublished));
        }
    }

    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected) return;
        TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
        await _client.ConnectAsync(_options, cancellationToken);
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var cycleStarted = Stopwatch.GetTimestamp();
        var failedSymbols = 0;

        foreach (var batch in _symbolGroups.Chunk(_options.MaxBatchSize))
        {
            var references = batch.Select(static x => x.Reference).ToArray();
            var operationStarted = Stopwatch.GetTimestamp();
            var results = await _client.ReadManyAsync(references, cancellationToken);
            if (results.Count != references.Length)
                throw new InvalidDataException("Logix protocol client returned an unexpected number of read results.");

            for (var index = 0; index < batch.Length; index++)
            {
                var group = batch[index];
                var result = results[index];
                var duration = Stopwatch.GetElapsedTime(operationStarted);
                RecordOperation(result.Succeeded, read: true, write: false, duration, result.Message);

                if (!result.Succeeded || result.NativeValue is null)
                {
                    failedSymbols++;
                    foreach (var binding in group.Bindings)
                        await PublishPointFailureAsync(binding, MapPointQuality(result.Error), cancellationToken);
                    continue;
                }

                foreach (var binding in group.Bindings)
                {
                    try
                    {
                        var canonical = LogixValueCodec.ToCanonicalValue(binding, result.NativeValue);
                        await PublishAsync(binding, canonical, TagQuality.Good, cancellationToken);
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
                    {
                        failedSymbols++;
                        RecordOperation(success: false, read: false, write: false, TimeSpan.Zero, ex.Message);
                        await PublishPointFailureAsync(binding, TagQuality.BadConfiguration, cancellationToken);
                    }
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        var scanDuration = Stopwatch.GetElapsedTime(cycleStarted);
        lock (_diagnosticsGate)
        {
            _pollCycles++;
            _lastScanDurationTicks = scanDuration.Ticks;
            if (failedSymbols == 0)
                _lastSuccessfulPollAt = now;
            else
            {
                _failedPollCycles++;
                _lastFailedPollAt = now;
            }
        }

        if (failedSymbols == 0)
        {
            TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, now, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        }
        else
        {
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Running,
                now,
                $"{failedSymbols} Logix physical symbol read(s) failed in the latest poll cycle.",
                Interlocked.Read(ref _updatesPublished));
        }
    }

    private async Task PublishAllCommunicationFailureAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in _bindings)
            await PublishPointFailureAsync(binding, TagQuality.BadCommunication, cancellationToken);
        lock (_diagnosticsGate)
        {
            _failedPollCycles++;
            _lastFailedPollAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task PublishPointFailureAsync(LogixTagBinding binding, TagQuality quality, CancellationToken cancellationToken)
    {
        _cache.TryGet(binding.Tag.Id, out var previous);
        await PublishAsync(binding, previous?.Value, quality, cancellationToken);
    }

    private async Task PublishAsync(LogixTagBinding binding, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var sample = new TagValue(binding.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(binding.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperation(bool success, bool read, bool write, TimeSpan duration, string? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            if (read) _readOperations++;
            if (write) _writeOperations++;
            if (duration > TimeSpan.Zero)
            {
                _lastOperationDurationTicks = duration.Ticks;
                _totalOperationDurationTicks += duration.Ticks;
                _operationDurationSamples++;
            }

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
                if (!string.IsNullOrWhiteSpace(error)) _lastError = Sanitize(error);
            }
        }
    }

    private void RecordFailureOnly(Exception error)
    {
        lock (_diagnosticsGate)
        {
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = Sanitize(error.Message);
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

    private static TagQuality MapPointQuality(LogixProtocolError error) => error switch
    {
        LogixProtocolError.SymbolNotFound or LogixProtocolError.TypeMismatch => TagQuality.BadConfiguration,
        LogixProtocolError.AccessDenied or LogixProtocolError.ConstantOrReadOnly => TagQuality.BadDevice,
        LogixProtocolError.None => TagQuality.Good,
        _ => TagQuality.BadDevice
    };

    private async Task SafeDisconnectAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected) return;
        try { await _client.DisconnectAsync(cancellationToken); }
        catch (Exception ex) when (IsTransportException(ex)) { RecordFailureOnly(ex); }
    }

    private static bool IsTransportException(Exception ex) =>
        ex is TimeoutException or SocketException or ObjectDisposedException ||
        ex is IOException and not LogixCipException;

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _client.DisposeAsync();
        _writeGate.Dispose();
    }

    private sealed record SymbolGroup(LogixSymbolReference Reference, IReadOnlyList<LogixTagBinding> Bindings);
}
