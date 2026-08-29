using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Bacnet;

public sealed class BacnetIpDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IBacnetSession _session;
    private readonly IReadOnlyList<BacnetPoint> _points;
    private readonly Dictionary<Guid, BacnetPoint> _pointsByTagId;
    private readonly HashSet<Guid> _covTagIds = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _nextCovFallbackPollAt = new();
    private readonly List<IDisposable> _covSubscriptions = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;
    private long _cycles;
    private long _requests;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _timeouts;
    private long _readOperations;
    private long _writeOperations;
    private long _updatesPublished;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private long _lastScanDurationTicks;

    public BacnetIpDriver(
        string driverId,
        string name,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<BacnetPoint> points,
        IBacnetSession session,
        TimeSpan? scanRate = null,
        TimeSpan? covFallbackPollInterval = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(session);

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _session = session;
        _points = points.ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one BACnet point is required.", nameof(points));
        foreach (var point in _points) point.Validate();
        if (_points.Select(x => x.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each BACnet point must reference a unique TAG ID.", nameof(points));
        if (_points.Select(x => x.Binding.DeviceInstance).Distinct().Count() != 1)
            throw new ArgumentException("One BACnet Data Source must target exactly one BACnet Device Instance.", nameof(points));

        _pointsByTagId = _points.ToDictionary(x => x.Tag.Id);
        ScanRate = scanRate ?? TimeSpan.FromSeconds(1);
        if (ScanRate <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(scanRate));
        CovFallbackPollInterval = covFallbackPollInterval ?? TimeSpan.FromSeconds(Math.Max(30d, ScanRate.TotalSeconds * 30d));
        if (CovFallbackPollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(covFallbackPollInterval));
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(x => x.Tag).ToArray();
    public TimeSpan ScanRate { get; }
    public TimeSpan CovFallbackPollInterval { get; }
    public uint DeviceInstance => _points[0].Binding.DeviceInstance;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionState(CommunicationDriverOperationalState.Starting);
        foreach (var point in _points) _registry.Register(point.Tag);

        await _session.StartAsync(cancellationToken).ConfigureAwait(false);
        foreach (var point in _points.Where(x => x.Binding.UseCov))
        {
            try
            {
                var subscription = await _session.TrySubscribeCovAsync(
                    point.Binding,
                    sample => HandleCovSampleAsync(point, sample),
                    cancellationToken).ConfigureAwait(false);
                if (subscription is not null)
                {
                    _covSubscriptions.Add(subscription);
                    _covTagIds.Add(point.Tag.Id);
                    _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow + CovFallbackPollInterval;
                }
            }
            catch
            {
                // Subscription is an optimization/capability. Polling remains authoritative fallback.
            }
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        TransitionState(CommunicationDriverOperationalState.Stopping);
        await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }
        foreach (var subscription in _covSubscriptions) subscription.Dispose();
        _covSubscriptions.Clear();
        _covTagIds.Clear();
        _nextCovFallbackPollAt.Clear();
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        TransitionState(CommunicationDriverOperationalState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"BACnet TAG '{tagId}' was not found in driver '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"BACnet TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable) throw new InvalidOperationException($"BACnet TAG '{point.Tag.Path}' is not writable.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var encoded = BacnetValueCodec.Encode(value, point.Tag.DataType, point.Binding);
            Interlocked.Increment(ref _requests);
            Interlocked.Increment(ref _writeOperations);
            await _session.WriteAsync(point.Binding, encoded, cancellationToken).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null);
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex);
            TransitionState(CommunicationDriverOperationalState.Degraded);
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
        var quality = BuildQualitySummary();
        lock (_diagnosticsGate)
        {
            var operations = _successfulOperations + _failedOperations;
            var average = operations == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationDurationTicks / operations);
            var failureRate = _recentFailures.Count == 0 ? 0d : _recentFailures.Count(x => x) / (double)_recentFailures.Count;
            var dataAge = _lastSuccessfulCommunicationAt.HasValue ? capturedAt - _lastSuccessfulCommunicationAt.Value : (TimeSpan?)null;
            var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["deviceInstance"] = DeviceInstance.ToString(CultureInfo.InvariantCulture),
                ["covTagCount"] = _covTagIds.Count.ToString(CultureInfo.InvariantCulture),
                ["polledTagCount"] = (_points.Count - _covTagIds.Count).ToString(CultureInfo.InvariantCulture),
                ["covFallbackPollSeconds"] = CovFallbackPollInterval.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                ["transport"] = "BACnet/IP UDP",
                ["bacnetSecureConnect"] = "not-implemented"
            };
            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                BacnetDriverDescriptor.DriverType,
                _runtimeInstanceId,
                Endpoint: $"device:{DeviceInstance}",
                _communicationState,
                _stateChangedAt,
                capturedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                dataAge,
                ScanRate,
                _lastOperationDurationTicks == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                average,
                _lastScanDurationTicks == 0 ? null : TimeSpan.FromTicks(_lastScanDurationTicks),
                failureRate,
                _points.Count,
                quality,
                new CommunicationDriverCounters(
                    _cycles,
                    _requests,
                    _successfulOperations,
                    _failedOperations,
                    _consecutiveFailures,
                    _timeouts,
                    Connections: _lastSuccessfulCommunicationAt.HasValue ? 1 : 0,
                    Disconnections: 0,
                    Reconnects: 0,
                    _readOperations,
                    _writeOperations,
                    _updatesPublished),
                protocolDetails);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        _writeGate.Dispose();
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ScanRate);
        while (!cancellationToken.IsCancellationRequested)
        {
            var scanStarted = Stopwatch.GetTimestamp();
            var now = DateTimeOffset.UtcNow;
            var polled = _points.Where(point => ShouldPoll(point, now)).ToArray();
            foreach (var point in polled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var succeeded = await PollPointAsync(point, cancellationToken).ConfigureAwait(false);
                if (_covTagIds.Contains(point.Tag.Id))
                {
                    _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow +
                        (succeeded ? CovFallbackPollInterval : ScanRate);
                }
            }
            Interlocked.Increment(ref _cycles);
            Interlocked.Exchange(ref _lastScanDurationTicks, Stopwatch.GetElapsedTime(scanStarted).Ticks);
            if (_failedOperations == 0 || _consecutiveFailures == 0)
                TransitionState(CommunicationDriverOperationalState.Healthy);

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }

    private bool ShouldPoll(BacnetPoint point, DateTimeOffset now)
    {
        if (!_covTagIds.Contains(point.Tag.Id)) return true;
        return !_nextCovFallbackPollAt.TryGetValue(point.Tag.Id, out var nextPoll) || now >= nextPoll;
    }

    private async Task<bool> PollPointAsync(BacnetPoint point, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _readOperations);
        try
        {
            var sample = await _session.ReadAsync(point.Binding, cancellationToken).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null);
            await PublishSampleAsync(point, sample).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex);
            _cache.TryGet(point.Tag.Id, out var current);
            await PublishAsync(point, current?.Value, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
            TransitionState(CommunicationDriverOperationalState.Degraded);
            return false;
        }
    }

    private async ValueTask HandleCovSampleAsync(BacnetPoint point, BacnetPropertyReadResult sample)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await PublishSampleAsync(point, sample).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null);
            _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow + CovFallbackPollInterval;
            TransitionState(CommunicationDriverOperationalState.Healthy);
        }
        catch (Exception ex)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex);
            _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow;
            TransitionState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
    }

    private async ValueTask PublishSampleAsync(BacnetPoint point, BacnetPropertyReadResult sample)
    {
        if (sample.Values.Count == 0)
            throw new InvalidOperationException($"BACnet read for '{point.Tag.Path}' returned no values.");
        var decoded = BacnetValueCodec.Decode(sample.Values[0], point.Tag.DataType, point.Binding);
        await PublishAsync(point, decoded, TagQuality.Good, CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask PublishAsync(BacnetPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var tagValue = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, tagValue, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperation(bool success, TimeSpan duration, Exception? error)
    {
        lock (_diagnosticsGate)
        {
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            if (success)
            {
                _successfulOperations++;
                _consecutiveFailures = 0;
                _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
                _lastError = null;
            }
            else
            {
                _failedOperations++;
                _consecutiveFailures++;
                _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
                _lastError = Sanitize(error?.Message);
                if (error is TimeoutException) _timeouts++;
            }
            _recentFailures.Enqueue(!success);
            while (_recentFailures.Count > 100) _recentFailures.Dequeue();
        }
    }

    private void TransitionState(CommunicationDriverOperationalState next)
    {
        lock (_diagnosticsGate)
        {
            if (_communicationState == next) return;
            _communicationState = next;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0; var badComm = 0; var uncertain = 0; var bad = 0; var badConfig = 0;
        var badDevice = 0; var stale = 0; var disabled = 0; var noSample = 0;
        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var current) || current is null) { noSample++; continue; }
            switch (current.Quality)
            {
                case TagQuality.Good: good++; break;
                case TagQuality.BadCommunication: badComm++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.Bad: bad++; break;
                case TagQuality.BadConfiguration: badConfig++; break;
                case TagQuality.BadDevice: badDevice++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
            }
        }
        return new CommunicationTagQualitySummary(good, badComm, uncertain, bad, badConfig, badDevice, stale, disabled, noSample);
    }

    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return message;
        var trimmed = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }
}
