using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
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
    private readonly HashSet<Guid> _covManagedTagIds = new();
    private readonly Dictionary<Guid, IDisposable> _covSubscriptions = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _nextCovFallbackPollAt = new();
    private readonly SemaphoreSlim _covLifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _covStateGate = new();
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
    private bool? _deviceReachable;
    private DateTimeOffset? _lastReachabilityEstablishedAt;
    private DateTimeOffset? _lastReachabilityLostAt;
    private DateTimeOffset? _nextCovRecreationAttemptAt;
    private int _covRecreatePending;
    private long _covRecreationAttempts;
    private long _covRecreationFailures;
    private long _covInitialCreateAttempts;
    private long _covInitialCreateFailures;
    private long _covInitialSubscribeRequests;
    private long _covInitialSubscribeFailures;
    private long _covRecreationCreateAttempts;
    private long _covRecreationCreateFailures;
    private long _covRecreationSubscribeRequests;
    private long _covRecreationSubscribeFailures;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
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
        TimeSpan? covFallbackPollInterval = null,
        TimeSpan? covRecreationRetryInterval = null)
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
        CovRecreationRetryInterval = covRecreationRetryInterval ?? TimeSpan.FromSeconds(5);
        if (CovRecreationRetryInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(covRecreationRetryInterval));
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(x => x.Tag).ToArray();
    public TimeSpan ScanRate { get; }
    public TimeSpan CovFallbackPollInterval { get; }
    public TimeSpan CovRecreationRetryInterval { get; }
    public uint DeviceInstance => _points[0].Binding.DeviceInstance;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionState(CommunicationDriverOperationalState.Starting);
        foreach (var point in _points) _registry.Register(point.Tag);

        await _session.StartAsync(cancellationToken).ConfigureAwait(false);
        await _covLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var point in _points.Where(x => x.Binding.UseCov))
            {
                await TryCreateCovSubscriptionNoGateAsync(
                    point,
                    markManaged: true,
                    kind: CovSubscriptionCreateKind.Initial,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _covLifecycleGate.Release();
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

        await _covLifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            IDisposable[] subscriptions;
            lock (_covStateGate)
            {
                subscriptions = _covSubscriptions.Values.ToArray();
                _covSubscriptions.Clear();
                _covTagIds.Clear();
                _covManagedTagIds.Clear();
            }
            foreach (var subscription in subscriptions)
                await DisposeCovSubscriptionAsync(subscription).ConfigureAwait(false);
        }
        finally
        {
            _covLifecycleGate.Release();
        }

        _nextCovFallbackPollAt.Clear();
        Interlocked.Exchange(ref _covRecreatePending, 0);
        lock (_diagnosticsGate)
        {
            _nextCovRecreationAttemptAt = null;
            _deviceReachable = null;
        }
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
        if (value is null)
            throw new InvalidOperationException("BACnet null writes are reserved for explicit priority relinquish. Use RelinquishAsync instead.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();
        var communicationAttempted = false;
        try
        {
            var encoded = BacnetValueCodec.Encode(value, point.Tag.DataType, point.Binding);
            Interlocked.Increment(ref _requests);
            Interlocked.Increment(ref _writeOperations);
            communicationAttempted = true;
            await _session.WriteAsync(point.Binding, encoded, cancellationToken).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null, communicationEvidence: true);
            await TryRecreateCovSubscriptionsIfPendingAsync(cancellationToken).ConfigureAwait(false);
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex, communicationEvidence: communicationAttempted);
            TransitionState(communicationAttempted && IsReachabilityFailure(ex)
                ? CommunicationDriverOperationalState.Reconnecting
                : CommunicationDriverOperationalState.Degraded);
            throw;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Explicitly relinquishes this point's configured BACnet command priority by
    /// writing BACnet NULL at that priority, then reads back the effective value.
    /// </summary>
    public async ValueTask RelinquishAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"BACnet TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable) throw new InvalidOperationException($"BACnet TAG '{point.Tag.Path}' is not writable.");
        if (!point.Binding.WritePriority.HasValue)
            throw new InvalidOperationException($"BACnet TAG '{point.Tag.Path}' requires an explicit write priority before it can be relinquished.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();
        try
        {
            Interlocked.Increment(ref _requests);
            Interlocked.Increment(ref _writeOperations);
            await _session.WriteAsync(
                point.Binding,
                BacnetValueCodec.EncodeRelinquish(point.Binding),
                cancellationToken).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null, communicationEvidence: true);

            // BACnet priority arbitration determines the effective value after a
            // relinquish. Never publish null as though it were that resulting value.
            await PollPointAsync(point, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex, communicationEvidence: true);
            TransitionState(IsReachabilityFailure(ex)
                ? CommunicationDriverOperationalState.Reconnecting
                : CommunicationDriverOperationalState.Degraded);
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
        var covCounts = GetCovCounts();
        lock (_diagnosticsGate)
        {
            var operations = _successfulOperations + _failedOperations;
            var average = operations == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationDurationTicks / operations);
            var failureRate = _recentFailures.Count == 0 ? 0d : _recentFailures.Count(x => x) / (double)_recentFailures.Count;
            var dataAge = _lastSuccessfulCommunicationAt.HasValue ? capturedAt - _lastSuccessfulCommunicationAt.Value : (TimeSpan?)null;
            var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["deviceInstance"] = DeviceInstance.ToString(CultureInfo.InvariantCulture),
                ["covTagCount"] = covCounts.Active.ToString(CultureInfo.InvariantCulture),
                ["covManagedTagCount"] = covCounts.Managed.ToString(CultureInfo.InvariantCulture),
                ["polledTagCount"] = (_points.Count - covCounts.Active).ToString(CultureInfo.InvariantCulture),
                ["covFallbackPollSeconds"] = CovFallbackPollInterval.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                ["covRecreationRetrySeconds"] = CovRecreationRetryInterval.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                ["covRecreationPending"] = Volatile.Read(ref _covRecreatePending) == 1 ? "true" : "false",
                ["covRecreationAttempts"] = Interlocked.Read(ref _covRecreationAttempts).ToString(CultureInfo.InvariantCulture),
                ["covRecreationFailures"] = Interlocked.Read(ref _covRecreationFailures).ToString(CultureInfo.InvariantCulture),
                ["transport"] = "BACnet/IP UDP",
                ["connectionModel"] = "device-reachability",
                ["deviceReachable"] = _deviceReachable.HasValue ? (_deviceReachable.Value ? "true" : "false") : "unknown",
                ["bacnetSecureConnect"] = "not-implemented"
            };
            if (_nextCovRecreationAttemptAt.HasValue)
                protocolDetails["covNextRecreationAttemptAtUtc"] = _nextCovRecreationAttemptAt.Value.ToString("O", CultureInfo.InvariantCulture);
            if (_lastReachabilityEstablishedAt.HasValue)
                protocolDetails["lastReachabilityEstablishedAtUtc"] = _lastReachabilityEstablishedAt.Value.ToString("O", CultureInfo.InvariantCulture);
            if (_lastReachabilityLostAt.HasValue)
                protocolDetails["lastReachabilityLostAtUtc"] = _lastReachabilityLostAt.Value.ToString("O", CultureInfo.InvariantCulture);
            AppendForeignDeviceRegistrationDiagnostics(protocolDetails);
            AppendCovSubscriptionDiagnostics(protocolDetails);
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
                    _connections,
                    _disconnections,
                    _reconnects,
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
        _covLifecycleGate.Dispose();
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
                if (IsCovActive(point.Tag.Id))
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
        if (!IsCovActive(point.Tag.Id)) return true;
        return !_nextCovFallbackPollAt.TryGetValue(point.Tag.Id, out var nextPoll) || now >= nextPoll;
    }

    private async Task<bool> PollPointAsync(BacnetPoint point, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _readOperations);
        var communicationCompleted = false;
        try
        {
            var sample = await _session.ReadAsync(point.Binding, cancellationToken).ConfigureAwait(false);
            communicationCompleted = true;
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null, communicationEvidence: true);
            await PublishSampleAsync(point, sample).ConfigureAwait(false);
            await TryRecreateCovSubscriptionsIfPendingAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex, communicationEvidence: !communicationCompleted);
            _cache.TryGet(point.Tag.Id, out var current);
            await PublishAsync(point, current?.Value, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
            TransitionState(!communicationCompleted && IsReachabilityFailure(ex)
                ? CommunicationDriverOperationalState.Reconnecting
                : CommunicationDriverOperationalState.Degraded);
            return false;
        }
    }

    private async ValueTask HandleCovSampleAsync(BacnetPoint point, BacnetPropertyReadResult sample)
    {
        var started = Stopwatch.GetTimestamp();
        RecordReachabilitySuccess(DateTimeOffset.UtcNow);
        try
        {
            await PublishSampleAsync(point, sample).ConfigureAwait(false);
            RecordOperation(true, Stopwatch.GetElapsedTime(started), null, communicationEvidence: false);
            _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow + CovFallbackPollInterval;
            await TryRecreateCovSubscriptionsIfPendingAsync(CancellationToken.None).ConfigureAwait(false);
            TransitionState(CommunicationDriverOperationalState.Healthy);
        }
        catch (Exception ex)
        {
            RecordOperation(false, Stopwatch.GetElapsedTime(started), ex, communicationEvidence: false);
            _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow;
            TransitionState(CommunicationDriverOperationalState.Degraded);
            throw;
        }
    }

    private async Task<bool> TryCreateCovSubscriptionNoGateAsync(
        BacnetPoint point,
        bool markManaged,
        CovSubscriptionCreateKind kind,
        CancellationToken cancellationToken)
    {
        RecordCovCreateAttempt(kind);
        IDisposable? subscription;
        try
        {
            subscription = await _session.TrySubscribeCovAsync(
                point.Binding,
                sample => HandleCovSampleAsync(point, sample),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            RecordCovCreateFailure(kind);
            return false;
        }

        // SystemIoBacnetSession returns normally only after one SubscribeCOV
        // request was emitted. Null means that request failed/rejected; failures
        // before request emission (for example device resolution) escape above.
        RecordCovSubscribeRequest(kind, failed: subscription is null);
        if (subscription is null)
        {
            RecordCovCreateFailure(kind);
            return false;
        }

        lock (_covStateGate)
        {
            _covSubscriptions[point.Tag.Id] = subscription;
            _covTagIds.Add(point.Tag.Id);
            if (markManaged) _covManagedTagIds.Add(point.Tag.Id);
        }
        _nextCovFallbackPollAt[point.Tag.Id] = DateTimeOffset.UtcNow + CovFallbackPollInterval;
        RecordReachabilitySuccess(DateTimeOffset.UtcNow);
        return true;
    }

    private async ValueTask TryRecreateCovSubscriptionsIfPendingAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _covRecreatePending) == 0) return;
        if (_cts?.IsCancellationRequested == true) return;

        lock (_diagnosticsGate)
        {
            if (_nextCovRecreationAttemptAt.HasValue && _nextCovRecreationAttemptAt.Value > DateTimeOffset.UtcNow)
                return;
        }

        await _covLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _covRecreatePending) == 0 || _cts?.IsCancellationRequested == true) return;
            lock (_diagnosticsGate)
            {
                if (_nextCovRecreationAttemptAt.HasValue && _nextCovRecreationAttemptAt.Value > DateTimeOffset.UtcNow)
                    return;
            }

            BacnetPoint[] targets;
            IDisposable[] oldSubscriptions;
            lock (_covStateGate)
            {
                targets = _covManagedTagIds
                    .Select(tagId => _pointsByTagId[tagId])
                    .ToArray();
                oldSubscriptions = _covSubscriptions.Values.ToArray();
                _covSubscriptions.Clear();
                _covTagIds.Clear();
            }

            if (targets.Length == 0)
            {
                Interlocked.Exchange(ref _covRecreatePending, 0);
                lock (_diagnosticsGate) _nextCovRecreationAttemptAt = null;
                return;
            }

            Interlocked.Increment(ref _covRecreationAttempts);
            foreach (var subscription in oldSubscriptions)
                await DisposeCovSubscriptionAsync(subscription).ConfigureAwait(false);

            var failures = 0;
            foreach (var point in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await TryCreateCovSubscriptionNoGateAsync(
                        point,
                        markManaged: false,
                        kind: CovSubscriptionCreateKind.Recreation,
                        cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    failures++;
                }
            }

            if (failures == 0)
            {
                Interlocked.Exchange(ref _covRecreatePending, 0);
                lock (_diagnosticsGate) _nextCovRecreationAttemptAt = null;
            }
            else
            {
                Interlocked.Increment(ref _covRecreationFailures);
                ScheduleNextCovRecreationAttempt(DateTimeOffset.UtcNow + CovRecreationRetryInterval);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Interlocked.Increment(ref _covRecreationFailures);
            ScheduleNextCovRecreationAttempt(DateTimeOffset.UtcNow + CovRecreationRetryInterval);
        }
        finally
        {
            _covLifecycleGate.Release();
        }
    }

    private void RecordCovCreateAttempt(CovSubscriptionCreateKind kind)
    {
        if (kind == CovSubscriptionCreateKind.Initial)
            Interlocked.Increment(ref _covInitialCreateAttempts);
        else
            Interlocked.Increment(ref _covRecreationCreateAttempts);
    }

    private void RecordCovCreateFailure(CovSubscriptionCreateKind kind)
    {
        if (kind == CovSubscriptionCreateKind.Initial)
            Interlocked.Increment(ref _covInitialCreateFailures);
        else
            Interlocked.Increment(ref _covRecreationCreateFailures);
    }

    private void RecordCovSubscribeRequest(CovSubscriptionCreateKind kind, bool failed)
    {
        if (kind == CovSubscriptionCreateKind.Initial)
        {
            Interlocked.Increment(ref _covInitialSubscribeRequests);
            if (failed) Interlocked.Increment(ref _covInitialSubscribeFailures);
        }
        else
        {
            Interlocked.Increment(ref _covRecreationSubscribeRequests);
            if (failed) Interlocked.Increment(ref _covRecreationSubscribeFailures);
        }
    }

    private static async ValueTask DisposeCovSubscriptionAsync(IDisposable subscription)
    {
        try
        {
            if (subscription is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else
                subscription.Dispose();
        }
        catch
        {
            // Remote cancellation is best-effort cleanup. Session-level COV
            // diagnostics preserve concrete cancel failures when available.
        }
    }

    private async ValueTask PublishSampleAsync(BacnetPoint point, BacnetPropertyReadResult sample)
    {
        if (sample.Values.Count == 0)
            throw new InvalidOperationException($"BACnet read for '{point.Tag.Path}' returned no values.");
        var decoded = BacnetValueCodec.Decode(sample.Values[0], point.Tag.DataType, point.Binding);
        var quality = BacnetQualityMapper.FromObjectState(sample.ObjectState);
        await PublishAsync(point, decoded, quality, CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask PublishAsync(BacnetPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var tagValue = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, tagValue, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperation(bool success, TimeSpan duration, Exception? error, bool communicationEvidence)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            if (success)
            {
                _successfulOperations++;
                _consecutiveFailures = 0;
                _lastSuccessfulCommunicationAt = now;
                _lastError = null;
                if (communicationEvidence) RecordReachabilitySuccessNoLock(now);
            }
            else
            {
                _failedOperations++;
                _consecutiveFailures++;
                _lastFailedCommunicationAt = now;
                _lastError = Sanitize(error?.Message);
                if (error is TimeoutException) _timeouts++;
                if (communicationEvidence && IsReachabilityFailure(error)) RecordReachabilityFailureNoLock(now);
            }
            _recentFailures.Enqueue(!success);
            while (_recentFailures.Count > 100) _recentFailures.Dequeue();
        }
    }

    private void RecordReachabilitySuccess(DateTimeOffset observedAt)
    {
        lock (_diagnosticsGate) RecordReachabilitySuccessNoLock(observedAt);
    }

    private void RecordReachabilitySuccessNoLock(DateTimeOffset observedAt)
    {
        if (_deviceReachable == true) return;
        _deviceReachable = true;
        _connections++;
        if (_connections > 1) _reconnects++;
        _lastReachabilityEstablishedAt = observedAt;
    }

    private void RecordReachabilityFailureNoLock(DateTimeOffset observedAt)
    {
        if (_deviceReachable == false) return;
        if (_deviceReachable == true) _disconnections++;
        _deviceReachable = false;
        _lastReachabilityLostAt = observedAt;
        if (HasActiveCovSubscriptions())
        {
            Interlocked.Exchange(ref _covRecreatePending, 1);
            _nextCovRecreationAttemptAt = observedAt;
        }
    }

    private void ScheduleNextCovRecreationAttempt(DateTimeOffset nextAttemptAt)
    {
        Interlocked.Exchange(ref _covRecreatePending, 1);
        lock (_diagnosticsGate) _nextCovRecreationAttemptAt = nextAttemptAt;
    }

    private bool HasActiveCovSubscriptions()
    {
        lock (_covStateGate) return _covTagIds.Count > 0;
    }

    private bool IsCovActive(Guid tagId)
    {
        lock (_covStateGate) return _covTagIds.Contains(tagId);
    }

    private (int Active, int Managed) GetCovCounts()
    {
        lock (_covStateGate) return (_covTagIds.Count, _covManagedTagIds.Count);
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

    private void AppendForeignDeviceRegistrationDiagnostics(IDictionary<string, string> protocolDetails)
    {
        if (_session is not IBacnetForeignDeviceRegistrationDiagnostics source) return;
        var snapshot = source.GetForeignDeviceRegistrationDiagnostics();
        protocolDetails["fdrConfigured"] = snapshot.Configured ? "true" : "false";
        if (snapshot.TtlSeconds.HasValue)
            protocolDetails["fdrTtlSeconds"] = snapshot.TtlSeconds.Value.ToString(CultureInfo.InvariantCulture);
        if (snapshot.RenewalInterval.HasValue)
            protocolDetails["fdrRenewalSeconds"] = snapshot.RenewalInterval.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        if (snapshot.RetryInterval.HasValue)
            protocolDetails["fdrRetrySeconds"] = snapshot.RetryInterval.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        protocolDetails["fdrRegistrationRequestsSent"] = snapshot.RegistrationRequestsSent.ToString(CultureInfo.InvariantCulture);
        protocolDetails["fdrRegistrationFailures"] = snapshot.RegistrationFailures.ToString(CultureInfo.InvariantCulture);
        if (snapshot.LastRegistrationRequestAt.HasValue)
            protocolDetails["fdrLastRegistrationRequestAtUtc"] = snapshot.LastRegistrationRequestAt.Value.ToString("O", CultureInfo.InvariantCulture);
        if (snapshot.NextRegistrationAttemptAt.HasValue)
            protocolDetails["fdrNextRegistrationAttemptAtUtc"] = snapshot.NextRegistrationAttemptAt.Value.ToString("O", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(snapshot.LastErrorType))
            protocolDetails["fdrLastErrorType"] = snapshot.LastErrorType;
    }

    private void AppendCovSubscriptionDiagnostics(IDictionary<string, string> protocolDetails)
    {
        var initialCreateAttempts = Interlocked.Read(ref _covInitialCreateAttempts);
        var initialCreateFailures = Interlocked.Read(ref _covInitialCreateFailures);
        var initialSubscribeRequests = Interlocked.Read(ref _covInitialSubscribeRequests);
        var initialSubscribeFailures = Interlocked.Read(ref _covInitialSubscribeFailures);
        var recreationCreateAttempts = Interlocked.Read(ref _covRecreationCreateAttempts);
        var recreationCreateFailures = Interlocked.Read(ref _covRecreationCreateFailures);
        var recreationSubscribeRequests = Interlocked.Read(ref _covRecreationSubscribeRequests);
        var recreationSubscribeFailures = Interlocked.Read(ref _covRecreationSubscribeFailures);

        protocolDetails["covInitialCreateAttempts"] = initialCreateAttempts.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covInitialCreateFailures"] = initialCreateFailures.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covInitialSubscribeRequests"] = initialSubscribeRequests.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covInitialSubscribeFailures"] = initialSubscribeFailures.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRecreationCreateAttempts"] = recreationCreateAttempts.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRecreationCreateFailures"] = recreationCreateFailures.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRecreationSubscribeRequests"] = recreationSubscribeRequests.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRecreationSubscribeFailures"] = recreationSubscribeFailures.ToString(CultureInfo.InvariantCulture);

        if (_session is not IBacnetCovSubscriptionDiagnostics source) return;
        var snapshot = source.GetCovSubscriptionDiagnostics();
        var createSubscribeRequests = initialSubscribeRequests + recreationSubscribeRequests;
        var createSubscribeFailures = initialSubscribeFailures + recreationSubscribeFailures;
        var renewalRequests = Math.Max(0L, snapshot.SubscribeRequests - createSubscribeRequests);
        var renewalFailures = Math.Max(0L, snapshot.SubscribeFailures - createSubscribeFailures);

        protocolDetails["covSessionActiveSubscriptions"] = snapshot.ActiveSubscriptions.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covSubscribeRequests"] = snapshot.SubscribeRequests.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covSubscribeFailures"] = snapshot.SubscribeFailures.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRenewalRequests"] = renewalRequests.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covRenewalFailures"] = renewalFailures.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covCancelRequests"] = snapshot.CancelRequests.ToString(CultureInfo.InvariantCulture);
        protocolDetails["covCancelFailures"] = snapshot.CancelFailures.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(snapshot.LastErrorType))
            protocolDetails["covLastErrorType"] = snapshot.LastErrorType;
    }

    private static bool IsReachabilityFailure(Exception? error)
        => error is TimeoutException or IOException or SocketException or ObjectDisposedException;

    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return message;
        var trimmed = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }

    private enum CovSubscriptionCreateKind
    {
        Initial,
        Recreation
    }
}