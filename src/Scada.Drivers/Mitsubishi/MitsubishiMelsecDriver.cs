using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mitsubishi;

/// <summary>Polling MELSEC driver using one persistent, single-flight SLMP 3E session.</summary>
public sealed class MitsubishiMelsecDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private const int RecentOutcomeWindow = 100;
    private readonly string _dataSourceKey;
    private readonly string _driverType;
    private readonly MitsubishiMelsecConnectionOptions _options;
    private readonly IReadOnlyList<MitsubishiMelsecPoint> _points;
    private readonly IReadOnlyList<MitsubishiMelsecPollBlock> _blocks;
    private readonly Dictionary<Guid, MitsubishiMelsecPoint> _byTagId;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly MitsubishiMelsecTcpSession _session;
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
    private long _confirmedReadbacks;
    private long _ambiguousWrites;
    private long _lastOperationTicks;
    private long _totalOperationTicks;
    private long _lastScanTicks;
    private int _operationSamples;
    private int _successfulBlocks;
    private int _failedBlocks;
    private int _consecutiveReconnectFailures;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessfulAt;
    private DateTimeOffset? _lastFailedAt;
    private string? _lastError;
    private string? _lastCommand;
    private bool _initialAcquisitionCompleted;
    private long _initialAcquisitionAttempts;

    public MitsubishiMelsecDriver(
        string dataSourceKey,
        string name,
        MitsubishiMelsecConnectionOptions options,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<MitsubishiMelsecPoint> points,
        Func<bool>? effectAuthority = null)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey)) throw new ArgumentException("Data Source key is required.", nameof(dataSourceKey));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        _dataSourceKey = dataSourceKey.Trim();
        _driverType = MitsubishiMelsecDriverDescriptorProvider.DriverTypeId;
        DriverId = _dataSourceKey;
        Name = name.Trim();
        _options = options;
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one MELSEC point is required.", nameof(points));
        if (_points.Select(point => point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each Mitsubishi TAG ID must be unique within the Data Source.", nameof(points));
        foreach (var point in _points) ValidatePoint(point);
        _byTagId = _points.ToDictionary(point => point.Tag.Id);
        _blocks = BuildPollBlocks(_points, options);
        _session = new MitsubishiMelsecTcpSession(options);
        _effectAuthority = effectAuthority ?? (() => true);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor.RuntimeCapabilities;
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
        // Activation requests can be short lived. Runtime owns this loop until its canonical stop.
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _cts;
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
        await _session.DisconnectAsync().ConfigureAwait(false);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        TransitionState(CommunicationDriverOperationalState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.ContainsKey(tagId)) throw new KeyNotFoundException($"MELSEC TAG '{tagId}' was not found in '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.TryGetValue(tagId, out var point)) throw new KeyNotFoundException($"MELSEC TAG '{tagId}' was not found in '{DriverId}'.");
        if (!point.Writable) throw new InvalidOperationException($"MELSEC TAG '{point.Tag.Path}' is not writable.");
        if (!_effectAuthority()) throw new InvalidOperationException("MELSEC write authority is not active for this Runtime instance.");

        var isBit = point.PhysicalType == MitsubishiMelsecPhysicalType.Bit;
        var expectedBytes = isBit ? Array.Empty<byte>() : MitsubishiMelsecValueCodec.Encode(point.PhysicalType, value, point.Transform);
        var bitValue = isBit && MitsubishiMelsecValueCodec.ToBit(value);
        var span = isBit ? 1 : MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType);
        var payload = isBit
            ? MitsubishiMelsecProtocolCodec.EncodePackedBits(new[] { bitValue })
            : expectedBytes;
        var writeStarted = Stopwatch.GetTimestamp();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_effectAuthority()) throw new InvalidOperationException("MELSEC write authority is no longer active.");
            var frame = MitsubishiMelsecProtocolCodec.BuildBatchWrite(
                _options.Route,
                _options.MonitoringTimerUnits,
                point.Address,
                checked((ushort)span),
                isBit,
                payload);
            var commandData = frame.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6);
            try
            {
                _lastCommand = "1401";
                var response = await _session.ExecuteAsync(
                    MitsubishiMelsecProtocolCodec.BatchWriteCommand,
                    isBit ? (ushort)1 : (ushort)0,
                    commandData,
                    writeCommand: true,
                    expectedDataLength: 0,
                    cancellationToken).ConfigureAwait(false);
                _ = response;
                RecordOperation(success: true, isRead: false, isWrite: true, Stopwatch.GetElapsedTime(writeStarted), null);
                // ACK is protocol completion evidence, not physical process-value truth. The poll path publishes observed values.
            }
            catch (MitsubishiMelsecProtocolException ex) when (ex.DispatchMayHaveOccurred)
            {
                await _session.DisconnectAsync().ConfigureAwait(false);
                try
                {
                    if (!_effectAuthority()) throw new InvalidOperationException("MELSEC authority was lost before readback.");
                    var actual = await ReadPointValueAsync(point, CancellationToken.None).ConfigureAwait(false);
                    var matches = isBit
                        ? actual.Bit == bitValue
                        : actual.RawBytes is not null && actual.RawBytes.AsSpan().SequenceEqual(expectedBytes);
                    if (matches)
                    {
                        Interlocked.Increment(ref _confirmedReadbacks);
                        await PublishAsync(point, actual.Value, TagQuality.Good, CancellationToken.None).ConfigureAwait(false);
                        RecordOperation(success: true, isRead: false, isWrite: true, Stopwatch.GetElapsedTime(writeStarted), null);
                        return;
                    }
                }
                catch (Exception readbackError) when (readbackError is not OperationCanceledException)
                {
                    lock (_diagnosticGate) _lastError = SanitizeError(readbackError);
                }
                Interlocked.Increment(ref _ambiguousWrites);
                RecordOperation(success: false, isRead: false, isWrite: true, Stopwatch.GetElapsedTime(writeStarted), ex);
                TransitionState(CommunicationDriverOperationalState.Degraded);
                throw new MitsubishiMelsecWriteOutcomeUnknownException(
                    "MELSEC write dispatch may have occurred, but bounded readback could not confirm the resulting value.",
                    ex.EndCode,
                    ex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                RecordOperation(success: false, isRead: false, isWrite: true, Stopwatch.GetElapsedTime(writeStarted), ex);
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
                ["familyProfile"] = _options.FamilyProfile.ToString(),
                ["route"] = $"net={_options.Route.NetworkNo:X2};station={_options.Route.StationNo:X2};moduleIo={_options.Route.ModuleIoNo:X4};multidrop={_options.Route.MultidropStationNo:X2}",
                ["frame"] = "3E",
                ["encoding"] = "binary",
                ["lastCommand"] = _lastCommand ?? string.Empty,
                ["lastEndCode"] = _session.LastEndCode?.ToString("X4", CultureInfo.InvariantCulture) ?? string.Empty,
                ["lastFailureKind"] = _session.LastFailureKind ?? string.Empty,
                ["lastRoundTripMilliseconds"] = _session.LastRoundTripTime?.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty,
                ["confirmedByReadback"] = Interlocked.Read(ref _confirmedReadbacks).ToString(CultureInfo.InvariantCulture),
                ["ambiguousWriteCount"] = Interlocked.Read(ref _ambiguousWrites).ToString(CultureInfo.InvariantCulture),
                ["pollBlockCount"] = _blocks.Count.ToString(CultureInfo.InvariantCulture),
                ["successfulBlocks"] = _successfulBlocks.ToString(CultureInfo.InvariantCulture),
                ["failedBlocks"] = _failedBlocks.ToString(CultureInfo.InvariantCulture),
                ["initialAcquisitionCompleted"] = _initialAcquisitionCompleted ? "true" : "false"
            };
            var failureRate = _recentFailures.Count == 0 ? 0 : _recentFailures.Count(value => value) / (double)_recentFailures.Count;
            var average = _operationSamples == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationTicks / _operationSamples);
            return new CommunicationDriverDiagnosticSnapshot(
                _dataSourceKey,
                Name,
                _driverType,
                _runtimeInstanceId,
                _options.SanitizedEndpoint,
                _communicationState,
                _stateChangedAt,
                now,
                _lastSuccessfulAt,
                _lastFailedAt,
                _lastError,
                _lastSuccessfulAt.HasValue ? now - _lastSuccessfulAt.Value : null,
                _options.ScanInterval,
                _operationSamples == 0 ? null : TimeSpan.FromTicks(_lastOperationTicks),
                average,
                _cycles == 0 ? null : TimeSpan.FromTicks(_lastScanTicks),
                failureRate,
                _points.Count,
                summary,
                new CommunicationDriverCounters(
                    _cycles,
                    _session.RequestCount,
                    _successfulOperations,
                    _failedOperations,
                    _consecutiveFailures,
                    _session.TimeoutCount,
                    _session.ConnectionCount,
                    _session.DisconnectCount,
                    _session.ReconnectCount,
                    _readOperations,
                    _writeOperations,
                    Interlocked.Read(ref _updatesPublished)),
                details);
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
            _dataSourceKey,
            _driverType,
            state,
            DateTimeOffset.UtcNow,
            _lastError,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["initialAcquisitionCompleted"] = _initialAcquisitionCompleted ? "true" : "false",
                ["initialAcquisitionAttempts"] = _initialAcquisitionAttempts.ToString(CultureInfo.InvariantCulture),
                ["sessionConnected"] = _session.IsConnected ? "true" : "false"
            });
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
        try
        {
            using var timer = new PeriodicTimer(_options.ScanInterval);
            while (!cancellationToken.IsCancellationRequested && _effectAuthority())
            {
                var failedBlocks = await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                if (!_effectAuthority()) break;
                if (!_session.IsConnected && failedBlocks > 0)
                {
                    var attempt = Interlocked.Increment(ref _consecutiveReconnectFailures);
                    var delayMilliseconds = Math.Min(5000, 250 * (1 << Math.Min(attempt - 1, 5)));
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);
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
        }
    }

    private async Task<int> PollOnceAsync(CancellationToken cancellationToken)
    {
        var scanStarted = Stopwatch.GetTimestamp();
        var failed = 0;
        for (var blockIndex = 0; blockIndex < _blocks.Count; blockIndex++)
        {
            var block = _blocks[blockIndex];
            cancellationToken.ThrowIfCancellationRequested();
            if (!_effectAuthority()) break;
            var operationStarted = Stopwatch.GetTimestamp();
            try
            {
                await PollBlockAsync(block, cancellationToken).ConfigureAwait(false);
                RecordOperation(true, true, false, Stopwatch.GetElapsedTime(operationStarted), null);
                lock (_diagnosticGate) _successfulBlocks++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or InvalidOperationException or ArgumentException)
            {
                failed++;
                RecordOperation(false, true, false, Stopwatch.GetElapsedTime(operationStarted), ex);
                lock (_diagnosticGate) _failedBlocks++;
                await PublishBlockFailureAsync(block, cancellationToken).ConfigureAwait(false);
                if (!_session.IsConnected)
                {
                    for (var remainingIndex = blockIndex + 1; remainingIndex < _blocks.Count; remainingIndex++)
                    {
                        var skipped = _blocks[remainingIndex];
                        failed++;
                        lock (_diagnosticGate) _failedBlocks++;
                        await PublishBlockFailureAsync(skipped, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
            }
        }
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticGate)
        {
            _cycles++;
            _lastScanTicks = Stopwatch.GetElapsedTime(scanStarted).Ticks;
            _initialAcquisitionAttempts++;
            if (failed == 0 && _effectAuthority())
            {
                _initialAcquisitionCompleted = true;
                _lastSuccessfulAt = now;
            }
            else _lastFailedAt = now;
        }
        if (!_effectAuthority()) return failed;
        if (failed == 0)
        {
            TransitionState(CommunicationDriverOperationalState.Healthy);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, now, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        }
        else
        {
            TransitionState(failed == _blocks.Count ? CommunicationDriverOperationalState.Reconnecting : CommunicationDriverOperationalState.Degraded);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, now, $"{failed} of {_blocks.Count} MELSEC poll block(s) failed.", Interlocked.Read(ref _updatesPublished));
        }
        return failed;
    }

    private async Task PublishBlockFailureAsync(MitsubishiMelsecPollBlock block, CancellationToken cancellationToken)
    {
        foreach (var point in block.Points)
        {
            _cache.TryGet(point.Tag.Id, out var previous);
            await PublishAsync(point, previous?.Value, TagQuality.BadCommunication, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PollBlockAsync(MitsubishiMelsecPollBlock block, CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) return;
        if (block.ReadBlocks is not null)
        {
            await PollReadBlocksAsync(block, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (block.IsRandom)
        {
            await PollRandomBlockAsync(block, cancellationToken).ConfigureAwait(false);
            return;
        }
        var count = checked((ushort)(block.EndAddress - block.StartAddress.Number + 1));
        _lastCommand = "0401";
        var request = MitsubishiMelsecProtocolCodec.BuildBatchRead(
            _options.Route, _options.MonitoringTimerUnits, block.StartAddress, count, block.IsBit);
        var data = request.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6);
        var response = await _session.ExecuteAsync(
            MitsubishiMelsecProtocolCodec.BatchReadCommand,
            block.IsBit ? (ushort)1 : (ushort)0,
            data,
            writeCommand: false,
            expectedDataLength: MitsubishiMelsecProtocolCodec.ExpectedBatchReadDataLength(count, block.IsBit),
            cancellationToken).ConfigureAwait(false);
        if (!_effectAuthority()) return;
        foreach (var point in block.Points)
        {
            object value;
            if (block.IsBit)
            {
                var bit = MitsubishiMelsecProtocolCodec.DecodePackedBit(response.Data, point.Address.Number - block.StartAddress.Number);
                value = bit;
            }
            else
            {
                var offsetWords = point.Address.Number - block.StartAddress.Number;
                var wordCount = MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType);
                var raw = response.Data.AsMemory(offsetWords * 2, wordCount * 2).ToArray();
                value = MitsubishiMelsecValueCodec.Decode(point.PhysicalType, raw, bitValue: false, point.Transform);
            }
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PollRandomBlockAsync(MitsubishiMelsecPollBlock block, CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) return;
        var words = block.Points.Where(point => MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) == 1).ToArray();
        var doubleWords = block.Points.Where(point => MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) == 2).ToArray();
        var request = MitsubishiMelsecProtocolCodec.BuildReadRandom(
            _options.Route,
            _options.MonitoringTimerUnits,
            words.Select(point => point.Address).ToArray(),
            doubleWords.Select(point => point.Address).ToArray());
        var expectedBytes = checked(words.Length * 2 + doubleWords.Length * 4);
        _lastCommand = "0403";
        var response = await _session.ExecuteAsync(
            MitsubishiMelsecProtocolCodec.ReadRandomCommand,
            0,
            request.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6),
            writeCommand: false,
            expectedDataLength: expectedBytes,
            cancellationToken).ConfigureAwait(false);
        if (!_effectAuthority()) return;
        var offset = 0;
        foreach (var point in words)
        {
            var raw = response.Data.AsMemory(offset, 2).ToArray();
            var value = MitsubishiMelsecValueCodec.Decode(point.PhysicalType, raw, bitValue: false, point.Transform);
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
            offset += 2;
        }
        foreach (var point in doubleWords)
        {
            var raw = response.Data.AsMemory(offset, 4).ToArray();
            var value = MitsubishiMelsecValueCodec.Decode(point.PhysicalType, raw, bitValue: false, point.Transform);
            await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
            offset += 4;
        }
    }

    private async Task PollReadBlocksAsync(MitsubishiMelsecPollBlock pollBlock, CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) return;
        var readBlocks = pollBlock.ReadBlocks!;
        var request = MitsubishiMelsecProtocolCodec.BuildReadBlock(_options.Route, _options.MonitoringTimerUnits, readBlocks);
        var expectedBytes = checked(readBlocks.Sum(block => (int)block.PointCount) * 2);
        _lastCommand = "0406";
        var response = await _session.ExecuteAsync(
            MitsubishiMelsecProtocolCodec.ReadBlockCommand,
            0,
            request.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6),
            writeCommand: false,
            expectedDataLength: expectedBytes,
            cancellationToken).ConfigureAwait(false);
        if (!_effectAuthority()) return;

        var payloadOffset = 0;
        foreach (var readBlock in readBlocks)
        {
            var blockBytes = checked(readBlock.PointCount * 2);
            var points = pollBlock.Points.Where(point =>
                point.Address.Number >= readBlock.StartAddress.Number &&
                point.Address.Number + MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) <= readBlock.StartAddress.Number + readBlock.PointCount);
            foreach (var point in points)
            {
                var offset = checked(payloadOffset + (point.Address.Number - readBlock.StartAddress.Number) * 2);
                var raw = response.Data.AsMemory(offset, MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) * 2).ToArray();
                var value = MitsubishiMelsecValueCodec.Decode(point.PhysicalType, raw, bitValue: false, point.Transform);
                await PublishAsync(point, value, TagQuality.Good, cancellationToken).ConfigureAwait(false);
            }
            payloadOffset += blockBytes;
        }
    }

    private async Task<(object Value, bool Bit, byte[]? RawBytes)> ReadPointValueAsync(
        MitsubishiMelsecPoint point,
        CancellationToken cancellationToken)
    {
        if (!_effectAuthority()) throw new InvalidOperationException("MELSEC authority is not active for readback.");
        var isBit = point.PhysicalType == MitsubishiMelsecPhysicalType.Bit;
        var span = checked((ushort)(isBit ? 1 : MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType)));
        _lastCommand = "0401";
        var request = MitsubishiMelsecProtocolCodec.BuildBatchRead(
            _options.Route, _options.MonitoringTimerUnits, point.Address, span, isBit);
        var response = await _session.ExecuteAsync(
            MitsubishiMelsecProtocolCodec.BatchReadCommand,
            isBit ? (ushort)1 : (ushort)0,
            request.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6),
            writeCommand: false,
            expectedDataLength: MitsubishiMelsecProtocolCodec.ExpectedBatchReadDataLength(span, isBit),
            cancellationToken).ConfigureAwait(false);
        if (isBit)
        {
            var bit = MitsubishiMelsecProtocolCodec.DecodePackedBit(response.Data, 0);
            return (bit, bit, null);
        }
        var raw = response.Data;
        return (MitsubishiMelsecValueCodec.Decode(point.PhysicalType, raw, false, point.Transform), false, raw);
    }

    private async Task PublishAsync(MitsubishiMelsecPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
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

    private static IReadOnlyList<MitsubishiMelsecPollBlock> BuildPollBlocks(
        IReadOnlyCollection<MitsubishiMelsecPoint> points,
        MitsubishiMelsecConnectionOptions options)
    {
        var result = new List<MitsubishiMelsecPollBlock>();
        foreach (var group in points.GroupBy(point => (point.Address.Area, point.Address.IsBitDevice)))
        {
            var ordered = group.OrderBy(point => point.Address.Number).ToArray();
            if (!group.Key.IsBitDevice && ordered.Length > 1)
            {
                var runs = BuildReadBlockDescriptors(ordered);
                var totalWords = ordered.Sum(point => MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType));
                var firstAddress = ordered[0].Address.Number;
                var lastAddress = ordered.Max(point => point.Address.Number + MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) - 1);
                var rangeWords = lastAddress - firstAddress + 1;
                var randomCost = 2 + ordered.Length * 4 + totalWords * 2;
                var batchCost = 6 + rangeWords * 2;
                var blockCost = 2 + runs.Count * 6 + totalWords * 2;
                if (runs.Count > 1 && runs.Count <= options.MaxBlocksPerRequest && totalWords <= options.MaxWordsPerRequest && totalWords <= 960 &&
                    (ordered.Length > options.MaxRandomPointsPerRequest || blockCost < randomCost))
                {
                    result.Add(new MitsubishiMelsecPollBlock(
                        ordered[0].Address, firstAddress, lastAddress, false, ordered, ReadBlocks: runs));
                    continue;
                }
                if (ordered.Length <= options.MaxRandomPointsPerRequest && randomCost < Math.Min(batchCost, blockCost))
                {
                    result.Add(new MitsubishiMelsecPollBlock(ordered[0].Address, firstAddress, lastAddress, false, ordered, IsRandom: true));
                    continue;
                }
            }

            var maxCount = group.Key.IsBitDevice ? options.MaxBitsPerRequest : options.MaxWordsPerRequest;
            var current = new List<MitsubishiMelsecPoint>();
            var start = 0;
            var end = -1;
            foreach (var point in ordered)
            {
                var span = point.Address.IsBitDevice ? 1 : MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType);
                var pointEnd = checked(point.Address.Number + span - 1);
                if (span > maxCount) throw new ArgumentException($"Configured request limit cannot contain TAG '{point.Tag.Path}' without splitting its physical value.");
                if (current.Count > 0 && (point.Address.Number > end + 1 || pointEnd - start + 1 > maxCount))
                {
                    result.Add(new MitsubishiMelsecPollBlock(current[0].Address, start, end, group.Key.IsBitDevice, current.ToArray()));
                    current.Clear();
                }
                if (current.Count == 0) { start = point.Address.Number; end = pointEnd; }
                else end = Math.Max(end, pointEnd);
                current.Add(point);
            }
            if (current.Count > 0)
                result.Add(new MitsubishiMelsecPollBlock(current[0].Address, start, end, group.Key.IsBitDevice, current.ToArray()));
        }
        return result;
    }

    private static IReadOnlyList<MitsubishiMelsecReadBlock> BuildReadBlockDescriptors(IReadOnlyList<MitsubishiMelsecPoint> points)
    {
        var blocks = new List<MitsubishiMelsecReadBlock>();
        var start = points[0].Address.Number;
        var end = start + MitsubishiMelsecValueCodec.WordSpan(points[0].PhysicalType) - 1;
        for (var index = 1; index < points.Count; index++)
        {
            var point = points[index];
            var pointEnd = point.Address.Number + MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) - 1;
            if (point.Address.Number > end + 1)
            {
                var runCount = end - start + 1;
                if (runCount > 960) return Array.Empty<MitsubishiMelsecReadBlock>();
                blocks.Add(new MitsubishiMelsecReadBlock(points[index - 1].Address with { Number = start }, checked((ushort)runCount)));
                start = point.Address.Number;
            }
            end = pointEnd;
        }
        var finalCount = end - start + 1;
        if (finalCount > 960) return Array.Empty<MitsubishiMelsecReadBlock>();
        blocks.Add(new MitsubishiMelsecReadBlock(points[^1].Address with { Number = start }, checked((ushort)finalCount)));
        return blocks;
    }

    private void ValidatePoint(MitsubishiMelsecPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.Address.IsBitDevice != (point.PhysicalType == MitsubishiMelsecPhysicalType.Bit))
            throw new ArgumentException($"MELSEC TAG '{point.Tag.Path}' physical type does not match its device storage area.");
        if (point.Tag.DataType != MitsubishiMelsecValueCodec.CanonicalDataType(point.PhysicalType))
            throw new ArgumentException($"MELSEC TAG '{point.Tag.Path}' canonical data type does not match {point.PhysicalType}.");
        if (point.Address.Number + Math.Max(0, MitsubishiMelsecValueCodec.WordSpan(point.PhysicalType) - 1) > point.Address.MaximumAddress)
            throw new ArgumentException($"MELSEC TAG '{point.Tag.Path}' value span exceeds its device area range.");
        if (point.PhysicalType == MitsubishiMelsecPhysicalType.Bit && point.Transform is { IsIdentity: false })
            throw new ArgumentException($"MELSEC bit TAG '{point.Tag.Path}' cannot use a byte/word transform.");
        if (point.Address.Area == MitsubishiMelsecDeviceArea.X && point.Writable)
            throw new ArgumentException($"MELSEC input TAG '{point.Tag.Path}' cannot be writable.");
        if (point.Writable && point.Tag.ReadOnly)
            throw new ArgumentException($"MELSEC TAG '{point.Tag.Path}' is read-only but its binding is marked writable.");
    }

    private static string SanitizeError(Exception error) =>
        error is MitsubishiMelsecProtocolException protocol
            ? $"{protocol.FailureKind}{(protocol.EndCode.HasValue ? $" (end code 0x{protocol.EndCode.Value:X4})" : string.Empty)}"
            : error is SocketException socket ? $"socket_error ({socket.SocketErrorCode})" : error.GetType().Name;
}

internal sealed record MitsubishiMelsecPollBlock(
    MitsubishiMelsecAddress StartAddress,
    int Start,
    int EndAddress,
    bool IsBit,
    IReadOnlyList<MitsubishiMelsecPoint> Points,
    bool IsRandom = false,
    IReadOnlyList<MitsubishiMelsecReadBlock>? ReadBlocks = null);
