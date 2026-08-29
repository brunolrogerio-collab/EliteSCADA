using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

public sealed class S7IsoDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private const int RecentWindow = 100;
    private readonly S7IsoConnectionOptions _options;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<S7IsoPoint> _points;
    private readonly Dictionary<Guid, S7IsoPoint> _byTagId;
    private readonly S7IsoTransport _transport;
    private readonly object _diagGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private CommunicationDriverOperationalState _communicationState;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessAt;
    private DateTimeOffset? _lastFailureAt;
    private string? _lastError;
    private long _cycles;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _readOperations;
    private long _writeOperations;
    private long _updatesPublished;
    private long _operationSamples;
    private long _lastOperationTicks;
    private long _totalOperationTicks;
    private long _lastScanTicks;

    public S7IsoDriver(
        string driverId,
        string name,
        S7IsoConnectionOptions options,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<S7IsoPoint> points,
        TimeSpan? scanRate = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);

        DriverId = driverId.Trim();
        Name = name.Trim();
        _options = options;
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one S7 ISO point is required.", nameof(points));
        foreach (var point in _points) point.Validate();
        if (_points.Select(point => point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each S7 ISO point must reference a unique TAG ID.", nameof(points));

        _byTagId = _points.ToDictionary(point => point.Tag.Id);
        _transport = new S7IsoTransport(options);
        ScanRate = scanRate ?? TimeSpan.FromSeconds(1);
        if (ScanRate <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(scanRate));

        var now = DateTimeOffset.UtcNow;
        _communicationState = CommunicationDriverOperationalState.Stopped;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, now);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Tag).ToArray();
    public TimeSpan ScanRate { get; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        SetCommunicationState(CommunicationDriverOperationalState.Starting);
        foreach (var point in _points) _registry.Register(point.Tag);
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null)
        {
            await _transport.DisconnectAsync(cancellationToken);
            return;
        }

        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        SetCommunicationState(CommunicationDriverOperationalState.Stopping);
        await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }
        await _transport.DisconnectAsync(cancellationToken);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        SetCommunicationState(CommunicationDriverOperationalState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"S7 ISO TAG '{tagId}' was not found in driver '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_byTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"S7 ISO TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable)
            throw new InvalidOperationException($"S7 ISO TAG '{point.Tag.Path}' is not writable.");
        if (!_options.WriteEnabled)
            throw new InvalidOperationException(
                $"S7 ISO writes are disabled for data source '{DriverId}'. Enable 'writeEnabled' explicitly before allowing writes.");

        var encoded = S7IsoValueCodec.Encode(point, value);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await _transport.WriteAsync(point, encoded, cancellationToken);
            await PublishAsync(point, S7IsoValueCodec.Decode(point, encoded), TagQuality.Good, cancellationToken);
            RecordOperations(1, 0, 0, 1, Stopwatch.GetElapsedTime(started), null);
            SetCommunicationState(CommunicationDriverOperationalState.Healthy);
        }
        catch (S7IsoProtocolException ex) when (ex.ReturnCode.HasValue)
        {
            await PublishPreviousAsync(point, MapReturnCodeQuality(ex.ReturnCode.Value), cancellationToken);
            RecordOperations(0, 1, 0, 1, Stopwatch.GetElapsedTime(started), ex);
            SetCommunicationState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
        catch (S7IsoConfigurationException ex)
        {
            await PublishPreviousAsync(point, TagQuality.BadConfiguration, cancellationToken);
            RecordOperations(0, 1, 0, 1, Stopwatch.GetElapsedTime(started), ex);
            SetCommunicationState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
        catch (Exception ex) when (IsCommunicationException(ex))
        {
            await PublishPreviousAsync(point, TagQuality.BadCommunication, cancellationToken);
            RecordOperations(0, 1, 0, 1, Stopwatch.GetElapsedTime(started), ex);
            SetCommunicationState(CommunicationDriverOperationalState.Reconnecting);
            throw;
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var now = DateTimeOffset.UtcNow;
        var transport = _transport.GetDiagnostics();
        var quality = BuildQualitySummary();
        lock (_diagGate)
        {
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = _options.Host,
                ["port"] = _options.Port.ToString(CultureInfo.InvariantCulture),
                ["cpuFamily"] = _options.CpuFamily.ToString(),
                ["connectionMode"] = _options.ConnectionMode.ToString(),
                ["sourceTsap"] = S7IsoConnectionOptions.FormatTsap(_options.EffectiveSourceTsap),
                ["destinationTsap"] = S7IsoConnectionOptions.FormatTsap(_options.EffectiveDestinationTsap),
                ["requestedPduSize"] = _options.RequestedPduSize.ToString(CultureInfo.InvariantCulture),
                ["negotiatedPduSize"] = transport.NegotiatedPduSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ["writeEnabled"] = _options.WriteEnabled ? "true" : "false",
                ["lastFailureKind"] = transport.LastFailureKind?.ToString() ?? string.Empty
            };
            if (_options.ConnectionMode == S7IsoConnectionMode.RackSlot)
            {
                details["rack"] = _options.Rack!.Value.ToString(CultureInfo.InvariantCulture);
                details["slot"] = _options.Slot!.Value.ToString(CultureInfo.InvariantCulture);
                details["connectionRole"] = _options.ConnectionRole.ToString();
            }

            var average = _operationSamples == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationTicks / _operationSamples);
            var failureRate = _recentFailures.Count == 0 ? 0d : _recentFailures.Count(value => value) / (double)_recentFailures.Count;
            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                "siemens.s7.iso",
                _runtimeId,
                _options.SanitizedEndpoint,
                _communicationState,
                _stateChangedAt,
                now,
                _lastSuccessAt,
                _lastFailureAt,
                _lastError,
                _lastSuccessAt.HasValue ? now - _lastSuccessAt.Value : null,
                ScanRate,
                _operationSamples == 0 ? null : TimeSpan.FromTicks(_lastOperationTicks),
                average,
                _cycles == 0 ? null : TimeSpan.FromTicks(_lastScanTicks),
                failureRate,
                _points.Count,
                quality,
                new CommunicationDriverCounters(
                    _cycles,
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
                details);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(ScanRate);
            while (!cancellationToken.IsCancellationRequested)
            {
                await PollOnceAsync(cancellationToken);
                if (!await timer.WaitForNextTickAsync(cancellationToken)) break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_diagGate)
            {
                _lastFailureAt = DateTimeOffset.UtcNow;
                _lastError = SanitizeError(ex);
            }
            SetCommunicationState(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, SanitizeError(ex), Interlocked.Read(ref _updatesPublished));
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var successes = 0;
        var failures = 0;
        var communicationFailure = false;
        string? lastError = null;

        try
        {
            var read = await _transport.ReadDetailedAsync(_points, cancellationToken);
            foreach (var configurationFailure in read.ConfigurationFailures)
            {
                failures++;
                lastError = configurationFailure.Value;
                await PublishPreviousAsync(configurationFailure.Key, TagQuality.BadConfiguration, cancellationToken);
            }

            foreach (var result in read.Items)
            {
                if (!result.Succeeded)
                {
                    failures++;
                    lastError = $"0x{result.ReturnCode:X2} {S7IsoProtocol.DescribeReturnCode(result.ReturnCode)}";
                    await PublishPreviousAsync(result.Point, MapReturnCodeQuality(result.ReturnCode), cancellationToken);
                    continue;
                }

                try
                {
                    var payload = result.Data ?? throw new S7IsoProtocolException("Successful S7 read omitted payload data.");
                    await PublishAsync(result.Point, S7IsoValueCodec.Decode(result.Point, payload), TagQuality.Good, cancellationToken);
                    successes++;
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
                {
                    failures++;
                    lastError = SanitizeError(ex);
                    await PublishPreviousAsync(result.Point, TagQuality.BadConfiguration, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (S7IsoConfigurationException ex)
        {
            failures = _points.Count;
            lastError = SanitizeError(ex);
            foreach (var point in _points)
                await PublishPreviousAsync(point, TagQuality.BadConfiguration, cancellationToken);
        }
        catch (Exception ex) when (IsCommunicationException(ex))
        {
            failures = _points.Count;
            communicationFailure = true;
            lastError = SanitizeError(ex);
            foreach (var point in _points)
                await PublishPreviousAsync(point, TagQuality.BadCommunication, cancellationToken);
        }

        var duration = Stopwatch.GetElapsedTime(started);
        RecordOperations(successes, failures, _points.Count, 0, duration, failures == 0 ? null : new IOException(lastError ?? "S7 read failure."));
        lock (_diagGate)
        {
            _cycles++;
            _lastScanTicks = duration.Ticks;
        }

        Status = new DriverStatus(
            DriverId,
            Name,
            DriverState.Running,
            DateTimeOffset.UtcNow,
            failures == 0 ? null : $"{failures} of {_points.Count} S7 point(s) failed. Last error: {lastError}",
            Interlocked.Read(ref _updatesPublished));

        SetCommunicationState(failures == 0
            ? CommunicationDriverOperationalState.Healthy
            : successes > 0
                ? CommunicationDriverOperationalState.Degraded
                : communicationFailure
                    ? CommunicationDriverOperationalState.Reconnecting
                    : CommunicationDriverOperationalState.Degraded);
    }

    private async Task PublishPreviousAsync(S7IsoPoint point, TagQuality quality, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        await PublishAsync(point, previous?.Value, quality, cancellationToken);
    }

    private async Task PublishAsync(S7IsoPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        await _cache.UpdateAsync(
            point.Tag,
            new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId),
            cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperations(int successes, int failures, int reads, int writes, TimeSpan duration, Exception? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagGate)
        {
            _successfulOperations += successes;
            _failedOperations += failures;
            _readOperations += reads;
            _writeOperations += writes;
            _operationSamples++;
            _lastOperationTicks = duration.Ticks;
            _totalOperationTicks += duration.Ticks;
            _recentFailures.Enqueue(failures > 0);
            while (_recentFailures.Count > RecentWindow) _recentFailures.Dequeue();

            if (successes > 0) _lastSuccessAt = now;
            if (failures > 0)
            {
                _consecutiveFailures++;
                _lastFailureAt = now;
                _lastError = SanitizeError(error);
            }
            else
            {
                _consecutiveFailures = 0;
            }
        }
    }

    private void SetCommunicationState(CommunicationDriverOperationalState state)
    {
        lock (_diagGate)
        {
            if (_communicationState == state) return;
            _communicationState = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var counts = new int[9];
        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var sample) || sample is null) { counts[8]++; continue; }
            switch (sample.Quality)
            {
                case TagQuality.Good: counts[0]++; break;
                case TagQuality.BadCommunication: counts[1]++; break;
                case TagQuality.Uncertain: counts[2]++; break;
                case TagQuality.Bad: counts[3]++; break;
                case TagQuality.BadConfiguration: counts[4]++; break;
                case TagQuality.BadDevice: counts[5]++; break;
                case TagQuality.Stale: counts[6]++; break;
                case TagQuality.Disabled: counts[7]++; break;
                default: counts[3]++; break;
            }
        }
        return new CommunicationTagQualitySummary(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6], counts[7], counts[8]);
    }

    private static TagQuality MapReturnCodeQuality(byte code) => code switch
    {
        0x05 or 0x06 or 0x07 or 0x0A => TagQuality.BadConfiguration,
        _ => TagQuality.BadDevice
    };

    private static bool IsCommunicationException(Exception ex) =>
        ex is IOException or TimeoutException or SocketException or ObjectDisposedException;

    private static string SanitizeError(Exception? error)
    {
        if (error is null) return string.Empty;
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        await _transport.DisposeAsync();
    }
}