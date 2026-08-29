using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpDriver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private const int RecentOutcomeWindow = 100;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<ModbusPoint> _points;
    private readonly IReadOnlyList<ModbusPollBlock> _pollBlocks;
    private readonly ModbusTcpTransport _transport;
    private readonly Dictionary<Guid, ModbusPoint> _pointsByTagId;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private readonly Queue<bool> _recentFailures = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _updatesPublished;
    private CommunicationDriverOperationalState _communicationState;
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;
    private long _pollCycles;
    private long _operationCount;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _readOperations;
    private long _writeOperations;
    private long _successfulPollBlocks;
    private long _failedPollBlocks;
    private long _failedPollCycles;
    private long _consecutiveFailedCycles;
    private DateTimeOffset? _lastSuccessfulPollAt;
    private DateTimeOffset? _lastFailedPollAt;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private long _lastScanDurationTicks;

    public ModbusTcpDriver(
        string driverId,
        string name,
        string host,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<ModbusPoint> points,
        int port = 502,
        TimeSpan? scanRate = null,
        TimeSpan? requestTimeout = null,
        int maxGapElements = 8)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        if (maxGapElements < 0) throw new ArgumentOutOfRangeException(nameof(maxGapElements));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one Modbus point is required.", nameof(points));
        foreach (var point in _points) point.Validate();
        if (_points.Select(x => x.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each Modbus point must reference a unique TAG ID.", nameof(points));

        _pointsByTagId = _points.ToDictionary(x => x.Tag.Id);
        _pollBlocks = BuildPollBlocks(_points, maxGapElements);
        _transport = new ModbusTcpTransport(host, port, requestTimeout);
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
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(x => x.Tag).ToArray();
    public TimeSpan ScanRate { get; }
    public IReadOnlyCollection<ModbusPollBlockInfo> PollBlocks => _pollBlocks
        .Select(x => new ModbusPollBlockInfo(x.UnitId, x.Area, x.StartAddress, x.Quantity, x.Points.Count))
        .ToArray();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        TransitionCommunicationState(CommunicationDriverOperationalState.Starting);
        foreach (var point in _points) _registry.Register(point.Tag);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        TransitionCommunicationState(CommunicationDriverOperationalState.Stopping);
        await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }
        await _transport.DisconnectAsync();
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        TransitionCommunicationState(CommunicationDriverOperationalState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"Modbus TAG '{tagId}' was not found in driver '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Modbus TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable)
            throw new InvalidOperationException($"Modbus TAG '{point.Tag.Path}' is not writable.");

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            Func<Task> writeOperation;
            if (point.Area == ModbusDataArea.Coil)
            {
                var encoded = ModbusValueCodec.EncodeBit(point, value);
                writeOperation = () => _transport.WriteSingleCoilAsync(point.UnitId, point.Address, encoded, cancellationToken);
            }
            else if (point.Area == ModbusDataArea.HoldingRegister && point.AddressSelector is not null)
            {
                writeOperation = async () =>
                {
                    var current = await _transport.ReadRegistersAsync(
                        point.UnitId,
                        ModbusDataArea.HoldingRegister,
                        point.Address,
                        1,
                        cancellationToken);
                    var updated = ModbusValueCodec.ApplyRegisterBit(point, current[0], value);
                    await _transport.WriteSingleRegisterAsync(point.UnitId, point.Address, updated, cancellationToken);
                };
            }
            else if (point.Area == ModbusDataArea.HoldingRegister)
            {
                var registers = ModbusValueCodec.EncodeRegisters(point, value);
                writeOperation = registers.Length == 1
                    ? () => _transport.WriteSingleRegisterAsync(point.UnitId, point.Address, registers[0], cancellationToken)
                    : () => _transport.WriteMultipleRegistersAsync(point.UnitId, point.Address, registers, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Modbus area '{point.Area}' is read-only.");
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                await writeOperation();
                RecordOperation(success: true, read: false, write: true, Stopwatch.GetElapsedTime(started), null);
            }
            catch (Exception ex) when (IsCommunicationException(ex))
            {
                RecordOperation(success: false, read: false, write: true, Stopwatch.GetElapsedTime(started), ex);
                TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
                throw;
            }

            await PublishAsync(point, value, TagQuality.Good, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var transport = _transport.GetDiagnostics();
        var quality = BuildQualitySummary();
        lock (_diagnosticsGate)
        {
            var averageDuration = _operationCount == 0
                ? (TimeSpan?)null
                : TimeSpan.FromTicks(_totalOperationDurationTicks / _operationCount);
            var failureRate = _recentFailures.Count == 0
                ? 0d
                : _recentFailures.Count(x => x) / (double)_recentFailures.Count;
            var dataReference = _lastSuccessfulPollAt ?? _lastSuccessfulCommunicationAt;
            var dataAge = dataReference.HasValue ? capturedAt - dataReference.Value : (TimeSpan?)null;
            var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = _transport.Host,
                ["port"] = _transport.Port.ToString(CultureInfo.InvariantCulture),
                ["requestTimeoutMs"] = _transport.RequestTimeout.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                ["pollBlockCount"] = _pollBlocks.Count.ToString(CultureInfo.InvariantCulture),
                ["unitIds"] = string.Join(",", _points.Select(x => x.UnitId).Distinct().OrderBy(x => x)),
                ["successfulPollBlocks"] = _successfulPollBlocks.ToString(CultureInfo.InvariantCulture),
                ["failedPollBlocks"] = _failedPollBlocks.ToString(CultureInfo.InvariantCulture),
                ["failedPollCycles"] = _failedPollCycles.ToString(CultureInfo.InvariantCulture),
                ["consecutiveFailedCycles"] = _consecutiveFailedCycles.ToString(CultureInfo.InvariantCulture)
            };

            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                "modbus.tcp",
                _runtimeInstanceId,
                $"{_transport.Host}:{_transport.Port}",
                _communicationState,
                _stateChangedAt,
                capturedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                dataAge,
                ScanRate,
                _operationCount == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                averageDuration,
                _pollCycles == 0 ? null : TimeSpan.FromTicks(_lastScanDurationTicks),
                failureRate,
                _points.Count,
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

    public ModbusTcpDiagnosticSnapshot GetModbusDiagnostics()
    {
        var transport = _transport.GetDiagnostics();
        lock (_diagnosticsGate)
        {
            return new ModbusTcpDiagnosticSnapshot(
                _transport.Host,
                _transport.Port,
                ScanRate,
                _transport.RequestTimeout,
                _pollBlocks.Count,
                _points.Select(x => x.UnitId).Distinct().OrderBy(x => x).ToArray(),
                _successfulPollBlocks,
                _failedPollBlocks,
                _failedPollCycles,
                _consecutiveFailedCycles,
                _lastSuccessfulPollAt,
                _lastFailedPollAt,
                _pollCycles == 0 ? null : TimeSpan.FromTicks(_lastScanDurationTicks),
                transport);
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
            RecordFailureOnly(ex);
            TransitionCommunicationState(CommunicationDriverOperationalState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, SanitizeError(ex), _updatesPublished);
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var cycleStarted = Stopwatch.GetTimestamp();
        var failedBlocks = 0;
        string? lastError = null;

        foreach (var block in _pollBlocks)
        {
            var operationStarted = Stopwatch.GetTimestamp();
            try
            {
                if (block.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
                    await PollBitsAsync(block, cancellationToken);
                else
                    await PollRegistersAsync(block, cancellationToken);
                RecordOperation(success: true, read: true, write: false, Stopwatch.GetElapsedTime(operationStarted), null);
                lock (_diagnosticsGate) _successfulPollBlocks++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsCommunicationException(ex))
            {
                failedBlocks++;
                lastError = SanitizeError(ex);
                RecordOperation(success: false, read: true, write: false, Stopwatch.GetElapsedTime(operationStarted), ex);
                lock (_diagnosticsGate) _failedPollBlocks++;
                foreach (var point in block.Points)
                    await PublishCommunicationFailureAsync(point, cancellationToken);
            }
        }

        var scanDuration = Stopwatch.GetElapsedTime(cycleStarted);
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            _pollCycles++;
            _lastScanDurationTicks = scanDuration.Ticks;
            if (failedBlocks == 0)
            {
                _consecutiveFailedCycles = 0;
                _lastSuccessfulPollAt = now;
            }
            else
            {
                _failedPollCycles++;
                _consecutiveFailedCycles++;
                _lastFailedPollAt = now;
            }
        }

        var message = failedBlocks == 0
            ? null
            : $"{failedBlocks} of {_pollBlocks.Count} Modbus poll block(s) failed. Last error: {lastError}";
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, message, _updatesPublished);
        if (failedBlocks == 0)
            TransitionCommunicationState(CommunicationDriverOperationalState.Healthy);
        else if (failedBlocks < _pollBlocks.Count)
            TransitionCommunicationState(CommunicationDriverOperationalState.Degraded);
        else
            TransitionCommunicationState(CommunicationDriverOperationalState.Reconnecting);
    }

    private async Task PollBitsAsync(ModbusPollBlock block, CancellationToken cancellationToken)
    {
        var values = await _transport.ReadBitsAsync(
            block.UnitId,
            block.Area,
            block.StartAddress,
            block.Quantity,
            cancellationToken);

        foreach (var point in block.Points)
        {
            var index = point.Address - block.StartAddress;
            await PublishAsync(point, ModbusValueCodec.DecodeBit(point, values[index]), TagQuality.Good, cancellationToken);
        }
    }

    private async Task PollRegistersAsync(ModbusPollBlock block, CancellationToken cancellationToken)
    {
        var registers = await _transport.ReadRegistersAsync(
            block.UnitId,
            block.Area,
            block.StartAddress,
            block.Quantity,
            cancellationToken);

        foreach (var point in block.Points)
        {
            var offset = point.Address - block.StartAddress;
            var value = ModbusValueCodec.DecodeRegisters(point, registers.AsSpan(offset, point.RegisterCount));
            await PublishAsync(point, value, TagQuality.Good, cancellationToken);
        }
    }

    private async Task PublishCommunicationFailureAsync(ModbusPoint point, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        await PublishAsync(point, previous?.Value, TagQuality.BadCommunication, cancellationToken);
    }

    private async Task PublishAsync(ModbusPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private void RecordOperation(bool success, bool read, bool write, TimeSpan duration, Exception? error)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            _operationCount++;
            if (read) _readOperations++;
            if (write) _writeOperations++;
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
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
                _lastError = SanitizeError(error);
            }
        }
    }

    private void RecordFailureOnly(Exception error)
    {
        lock (_diagnosticsGate)
        {
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = SanitizeError(error);
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

    private static bool IsCommunicationException(Exception ex) =>
        ex is IOException or TimeoutException or SocketException or ObjectDisposedException;

    private static string SanitizeError(Exception? error)
    {
        if (error is null) return string.Empty;
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }

    private static IReadOnlyList<ModbusPollBlock> BuildPollBlocks(IReadOnlyList<ModbusPoint> points, int maxGapElements)
    {
        var result = new List<ModbusPollBlock>();
        foreach (var group in points.GroupBy(x => new { x.UnitId, x.Area }))
        {
            var ordered = group.OrderBy(x => x.Address).ThenBy(x => x.EndAddressExclusive).ToArray();
            var current = new List<ModbusPoint>();
            var start = 0;
            var end = 0;
            var maxQuantity = group.Key.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput ? 2000 : 125;

            foreach (var point in ordered)
            {
                if (current.Count == 0)
                {
                    current.Add(point);
                    start = point.Address;
                    end = point.EndAddressExclusive;
                    continue;
                }

                var gap = Math.Max(0, point.Address - end);
                var proposedEnd = Math.Max(end, point.EndAddressExclusive);
                var proposedQuantity = proposedEnd - start;
                if (gap <= maxGapElements && proposedQuantity <= maxQuantity)
                {
                    current.Add(point);
                    end = proposedEnd;
                }
                else
                {
                    result.Add(CreateBlock(group.Key.UnitId, group.Key.Area, start, end, current));
                    current = new List<ModbusPoint> { point };
                    start = point.Address;
                    end = point.EndAddressExclusive;
                }
            }

            if (current.Count > 0)
                result.Add(CreateBlock(group.Key.UnitId, group.Key.Area, start, end, current));
        }

        return result;
    }

    private static ModbusPollBlock CreateBlock(byte unitId, ModbusDataArea area, int start, int end, IReadOnlyList<ModbusPoint> points) =>
        new(unitId, area, checked((ushort)start), checked((ushort)(end - start)), points.ToArray());

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        await _transport.DisposeAsync();
        _writeGate.Dispose();
    }

    private sealed record ModbusPollBlock(
        byte UnitId,
        ModbusDataArea Area,
        ushort StartAddress,
        ushort Quantity,
        IReadOnlyList<ModbusPoint> Points);
}

public sealed record ModbusPollBlockInfo(
    byte UnitId,
    ModbusDataArea Area,
    ushort StartAddress,
    ushort Quantity,
    int PointCount);
