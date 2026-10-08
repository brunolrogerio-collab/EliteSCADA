using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Panasonic;

/// <summary>Polling MEWTOCOL-COM driver integrated with the canonical TAG and diagnostics path.</summary>
public sealed class PanasonicMewtocolDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private const int RecentOutcomeWindow = 100;
    private readonly string _dataSourceKey;
    private readonly string _driverType;
    private readonly PanasonicMewtocolConnectionOptions _options;
    private readonly IReadOnlyList<PanasonicMewtocolPoint> _points;
    private readonly IReadOnlyList<PanasonicMewtocolPollBatch> _batches;
    private readonly Dictionary<Guid, PanasonicMewtocolPoint> _byTagId;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IPanasonicMewtocolSession _session;
    private readonly Func<bool> _effectAuthority;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _diagnosticGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _updatesPublished;
    private long _cycles;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _readOperations;
    private long _writeOperations;
    private long _ambiguousWrites;
    private long _lastOperationTicks;
    private long _totalOperationTicks;
    private long _lastScanTicks;
    private int _operationSamples;
    private int _successfulBatches;
    private int _failedBatches;
    private int _consecutiveReconnectFailures;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessfulAt;
    private DateTimeOffset? _lastFailedAt;
    private string? _lastError;
    private string? _lastCommand;
    private bool _initialAcquisitionCompleted;
    private long _initialAcquisitionAttempts;

    public PanasonicMewtocolDriver(
        string dataSourceKey,
        string name,
        string driverType,
        PanasonicMewtocolConnectionOptions options,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<PanasonicMewtocolPoint> points,
        IEnumerable<PanasonicMewtocolPollBatch> batches,
        IPanasonicMewtocolSession session,
        Func<bool>? effectAuthority = null)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey)) throw new ArgumentException("Data Source key is required.", nameof(dataSourceKey));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        _ = PanasonicMewtocolDriverDescriptorProvider.For(driverType);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(batches);
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dataSourceKey = dataSourceKey.Trim();
        _driverType = driverType;
        DriverId = _dataSourceKey;
        Name = name.Trim();
        _options = options;
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        _batches = batches.ToArray();
        if (_points.Count == 0 || _batches.Count == 0) throw new ArgumentException("At least one Panasonic point and poll batch are required.", nameof(points));
        if (_points.Select(point => point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each Panasonic TAG ID must be unique within the Data Source.", nameof(points));
        _byTagId = _points.ToDictionary(point => point.Tag.Id);
        _effectAuthority = effectAuthority ?? (() => true);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => PanasonicMewtocolDriverDescriptorProvider.For(_driverType).RuntimeCapabilities;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Tag).ToArray();
    public TimeSpan ScanRate => _options.ScanInterval;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;
        if (!_effectAuthority())
        {
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
            TransitionState(CommunicationDriverOperationalState.Stopped);
            return Task.CompletedTask;
        }
        foreach (var point in _points)
            if (!_registry.TryGet(point.Tag.Id, out _)) _registry.Register(point.Tag);
        _initialAcquisitionCompleted = false;
        _initialAcquisitionAttempts = 0;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionState(CommunicationDriverOperationalState.Starting);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _cts;
        try
        {
            if (cts is not null)
            {
                Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
                TransitionState(CommunicationDriverOperationalState.Stopping);
                await cts.CancelAsync().ConfigureAwait(false);
                if (_loop is not null)
                {
                    try { await _loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
                }
            }
        }
        finally
        {
            await _session.DisconnectAsync().ConfigureAwait(false);
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            TransitionState(CommunicationDriverOperationalState.Stopped);
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.ContainsKey(tagId)) throw new KeyNotFoundException($"Panasonic TAG '{tagId}' was not found in '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.TryGetValue(tagId, out var point)) throw new KeyNotFoundException($"Panasonic TAG '{tagId}' was not found in '{DriverId}'.");
        if (!point.Writable) throw new InvalidOperationException($"Panasonic TAG '{point.Tag.Path}' is not writable.");
        if (!_effectAuthority()) throw new InvalidOperationException("Panasonic write authority is not active for this Runtime instance.");

        var request = point.PhysicalType == PanasonicMewtocolPhysicalType.Boolean
            ? PanasonicMewtocolProtocolCodec.BuildWriteContact(_options.Station, point.Address,
                PanasonicMewtocolValueCodec.ToBoolean(value), _options.FrameMode)
            : PanasonicMewtocolProtocolCodec.BuildWriteWord(_options.Station, point.Address,
                PanasonicMewtocolValueCodec.EncodeWord(value, point.PhysicalType, point.Transform), _options.FrameMode);
        var command = point.PhysicalType == PanasonicMewtocolPhysicalType.Boolean ? "WCS" : "WD";
        var responseCode = point.PhysicalType == PanasonicMewtocolPhysicalType.Boolean ? "WC" : "WD";
        var started = Stopwatch.GetTimestamp();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_effectAuthority()) throw new InvalidOperationException("Panasonic write authority is no longer active.");
            _lastCommand = command;
            try
            {
                _ = await _session.ExecuteAsync(request, responseCode, expectedDataLength: 0, writeCommand: true, cancellationToken).ConfigureAwait(false);
                RecordOperation(true, false, true, Stopwatch.GetElapsedTime(started), null);
                // Protocol ACK confirms command handling only. The polling path publishes later device observations.
            }
            catch (PanasonicMewtocolProtocolException ex) when (ex.DispatchMayHaveOccurred && ex.FailureKind != "plc_error")
            {
                Interlocked.Increment(ref _ambiguousWrites);
                RecordOperation(false, false, true, Stopwatch.GetElapsedTime(started), ex);
                TransitionState(CommunicationDriverOperationalState.Degraded);
                throw new PanasonicMewtocolWriteOutcomeUnknownException(
                    "MEWTOCOL-COM write dispatch may have occurred, but its outcome was not confirmed. No retry was sent.", ex.ErrorCode, ex);
            }
            catch (Exception ex)
            {
                RecordOperation(false, false, true, Stopwatch.GetElapsedTime(started), ex);
                TransitionState(CommunicationDriverOperationalState.Degraded);
                throw;
            }
        }
        finally { _writeGate.Release(); }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var now = DateTimeOffset.UtcNow;
        var summary = BuildQualitySummary();
        lock (_diagnosticGate)
        {
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["protocol"] = "MEWTOCOL-COM",
                ["transport"] = _options.Transport.ToString(),
                ["familyProfile"] = _options.FamilyProfile.ToString(),
                ["frameMode"] = _options.FrameMode.ToString(),
                ["lastCommand"] = _lastCommand ?? string.Empty,
                ["lastFailureKind"] = _session.LastFailureKind ?? string.Empty,
                ["lastRoundTripMilliseconds"] = _session.LastRoundTripTime?.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty,
                ["ambiguousWriteCount"] = Interlocked.Read(ref _ambiguousWrites).ToString(CultureInfo.InvariantCulture),
                ["pollBatchCount"] = _batches.Count.ToString(CultureInfo.InvariantCulture),
                ["successfulBatches"] = _successfulBatches.ToString(CultureInfo.InvariantCulture),
                ["failedBatches"] = _failedBatches.ToString(CultureInfo.InvariantCulture),
                ["initialAcquisitionCompleted"] = _initialAcquisitionCompleted ? "true" : "false"
            };
            var failureRate = _recentFailures.Count == 0 ? 0 : _recentFailures.Count(value => value) / (double)_recentFailures.Count;
            var average = _operationSamples == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationTicks / _operationSamples);
            return new CommunicationDriverDiagnosticSnapshot(
                _dataSourceKey, Name, _driverType, _runtimeInstanceId, _options.SanitizedEndpoint,
                _communicationState, _stateChangedAt, now, _lastSuccessfulAt, _lastFailedAt, _lastError,
                _lastSuccessfulAt.HasValue ? now - _lastSuccessfulAt.Value : null, _options.ScanInterval,
                _operationSamples == 0 ? null : TimeSpan.FromTicks(_lastOperationTicks), average,
                _cycles == 0 ? null : TimeSpan.FromTicks(_lastScanTicks), failureRate, _points.Count, summary,
                new CommunicationDriverCounters(
                    _cycles, _session.RequestCount, _successfulOperations, _failedOperations, _consecutiveFailures,
                    _session.TimeoutCount, _session.ConnectionCount, _session.DisconnectCount, _session.ReconnectCount,
                    _readOperations, _writeOperations, Interlocked.Read(ref _updatesPublished)), details);
        }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var state = _communicationState switch
        {
            CommunicationDriverOperationalState.Stopped => CommunicationDriverReadinessState.Stopped,
            CommunicationDriverOperationalState.Starting => CommunicationDriverReadinessState.Starting,
            CommunicationDriverOperationalState.Faulted => CommunicationDriverReadinessState.Faulted,
            _ when _initialAcquisitionCompleted => CommunicationDriverReadinessState.Ready,
            _ => CommunicationDriverReadinessState.NotStarted
        };
        return new CommunicationDriverReadinessSnapshot(
            _dataSourceKey, _driverType, state, DateTimeOffset.UtcNow, _lastError,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["initialAcquisitionCompleted"] = _initialAcquisitionCompleted ? "true" : "false",
                ["initialAcquisitionAttempts"] = _initialAcquisitionAttempts.ToString(CultureInfo.InvariantCulture),
                ["sessionConnected"] = _session.IsConnected ? "true" : "false"
            });
    }

    public async ValueTask DisposeAsync()
    {
        Exception? stopFailure = null;
        try { await StopAsync().ConfigureAwait(false); }
        catch (Exception ex) { stopFailure = ex; }
        Exception? disposeFailure = null;
        try { await _session.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { disposeFailure = ex; }
        _writeGate.Dispose();
        _cts?.Dispose();
        if (stopFailure is not null && disposeFailure is not null) throw new AggregateException("Panasonic stop and session cleanup both failed.", stopFailure, disposeFailure);
        if (stopFailure is not null) throw new InvalidOperationException("Panasonic stop failed; session disposal was still attempted.", stopFailure);
        if (disposeFailure is not null) throw new InvalidOperationException("Panasonic session disposal failed.", disposeFailure);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.ScanInterval);
            while (!cancellationToken.IsCancellationRequested && _effectAuthority())
            {
                var failed = await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                if (!_effectAuthority()) break;
                if (!_session.IsConnected && failed > 0)
                {
                    var attempt = Interlocked.Increment(ref _consecutiveReconnectFailures);
                    var delay = TimeSpan.FromMilliseconds(Math.Min(5000, 250 * (1 << Math.Min(attempt - 1, 5))));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                Interlocked.Exchange(ref _consecutiveReconnectFailures, 0);
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) break;
            }
            if (!_effectAuthority())
            {
                await _session.DisconnectAsync().ConfigureAwait(false);
                Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
                TransitionState(CommunicationDriverOperationalState.Stopped);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            RecordOperation(false, true, false, TimeSpan.Zero, ex);
            TransitionState(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, SanitizeError(ex), Interlocked.Read(ref _updatesPublished));
            try { await _session.DisconnectAsync().ConfigureAwait(false); }
            catch (Exception cleanupError) { lock (_diagnosticGate) _lastError = $"{_lastError}; cleanup={SanitizeError(cleanupError)}"; }
        }
    }

    private async Task<int> PollOnceAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var failed = 0;
        foreach (var batch in _batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_effectAuthority()) break;
            var operationStarted = Stopwatch.GetTimestamp();
            try
            {
                await PollBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                RecordOperation(true, true, false, Stopwatch.GetElapsedTime(operationStarted), null);
                lock (_diagnosticGate) _successfulBatches++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                failed++;
                RecordOperation(false, true, false, Stopwatch.GetElapsedTime(operationStarted), ex);
                lock (_diagnosticGate) _failedBatches++;
                await PublishBatchFailureAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticGate)
        {
            _cycles++;
            _lastScanTicks = Stopwatch.GetElapsedTime(started).Ticks;
            _initialAcquisitionAttempts++;
            if (failed == 0 && _effectAuthority())
            {
                _initialAcquisitionCompleted = true;
                _lastSuccessfulAt = now;
            }
            else if (failed > 0) _lastFailedAt = now;
        }
        if (!_effectAuthority()) return failed;
        if (failed == 0)
        {
            TransitionState(CommunicationDriverOperationalState.Healthy);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, now, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        }
        else
        {
            TransitionState(failed == _batches.Count ? CommunicationDriverOperationalState.Reconnecting : CommunicationDriverOperationalState.Degraded);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, now, $"{failed} of {_batches.Count} Panasonic poll batch(es) failed.", Interlocked.Read(ref _updatesPublished));
        }
        return failed;
    }

    private async Task PollBatchAsync(PanasonicMewtocolPollBatch batch, CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) return;
        var first = batch.Points.First();
        var request = batch.IsContact
            ? PanasonicMewtocolProtocolCodec.BuildReadContacts(_options.Station, batch.Points.Select(point => point.Address).ToArray(), _options.FrameMode)
            : PanasonicMewtocolProtocolCodec.BuildReadWords(_options.Station, first.Address, batch.Count, _options.FrameMode);
        var responseCode = batch.IsContact ? "RC" : "RD";
        var expectedLength = batch.IsContact ? batch.Count : checked(batch.Count * 4);
        _lastCommand = batch.IsContact ? (batch.Count == 1 ? "RCS" : "RCP") : "RD";
        var response = await _session.ExecuteAsync(request, responseCode, expectedLength, writeCommand: false, cancellationToken).ConfigureAwait(false);
        if (!_effectAuthority()) return;
        foreach (var point in batch.Points)
        {
            object value;
            if (batch.IsContact)
            {
                var statusIndex = batch.Points.ToList().FindIndex(candidate => candidate.Tag.Id == point.Tag.Id);
                var status = response.Data[statusIndex];
                if (status is not ('0' or '1')) throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM contact response contains a value other than 0 or 1.", "invalid_contact_value");
                value = status == '1';
            }
            else
            {
                var offset = checked((point.Address.Number - batch.StartAddress) * 4);
                var rawWord = Convert.FromHexString(response.Data.AsSpan(offset, 4));
                value = PanasonicMewtocolValueCodec.DecodeWord(rawWord, point.PhysicalType, point.Transform);
            }
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishBatchFailureAsync(PanasonicMewtocolPollBatch batch, CancellationToken cancellationToken)
    {
        foreach (var point in batch.Points)
        {
            _cache.TryGet(point.Tag.Id, out var previous);
            await PublishAsync(point, previous?.Value, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(PanasonicMewtocolPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperation(bool success, bool isRead, bool isWrite, TimeSpan duration, Exception? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticGate)
        {
            _operationSamples++;
            _lastOperationTicks = duration.Ticks;
            _totalOperationTicks += duration.Ticks;
            _recentFailures.Enqueue(!success);
            while (_recentFailures.Count > RecentOutcomeWindow) _recentFailures.Dequeue();
            if (success)
            {
                _successfulOperations++;
                _consecutiveFailures = 0;
                _lastSuccessfulAt = now;
                _lastError = null;
            }
            else
            {
                _failedOperations++;
                _consecutiveFailures++;
                _lastFailedAt = now;
                _lastError = error is null ? null : SanitizeError(error);
            }
            if (isRead) _readOperations++;
            if (isWrite) _writeOperations++;
        }
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var values = _points.Select(point => _cache.TryGet(point.Tag.Id, out var value) ? value : null).ToArray();
        return new CommunicationTagQualitySummary(
            values.Count(value => value?.Quality == TagQuality.Good),
            values.Count(value => value?.Quality == TagQuality.BadCommunication),
            values.Count(value => value?.Quality == TagQuality.Uncertain),
            values.Count(value => value?.Quality is TagQuality.Bad or TagQuality.Unavailable),
            values.Count(value => value?.Quality == TagQuality.BadConfiguration),
            values.Count(value => value?.Quality == TagQuality.BadDevice),
            values.Count(value => value?.Quality == TagQuality.Stale),
            values.Count(value => value?.Quality == TagQuality.Disabled),
            values.Count(value => value is null));
    }

    private void TransitionState(CommunicationDriverOperationalState state)
    {
        lock (_diagnosticGate)
        {
            if (_communicationState == state) return;
            _communicationState = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string SanitizeError(Exception error) => error switch
    {
        PanasonicMewtocolProtocolException protocol => $"{protocol.FailureKind}{(protocol.ErrorCode is null ? string.Empty : $" (0x{protocol.ErrorCode})")}",
        PanasonicMewtocolWriteOutcomeUnknownException => "write_outcome_unknown",
        SocketException socket => $"socket_error ({socket.SocketErrorCode})",
        TimeoutException => "timeout",
        _ => error.GetType().Name
    };
}
