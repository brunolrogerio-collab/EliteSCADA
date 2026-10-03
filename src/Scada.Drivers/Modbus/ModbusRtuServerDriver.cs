using System.Diagnostics;
using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Modbus;

public sealed class ModbusRtuServerDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly HostSerialBusCoordinator _coordinator;
    private readonly HostSerialLineSettings _serialSettings;
    private readonly TimeSpan _frameTimeout;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<ModbusServerPoint> _points;
    private readonly Dictionary<Guid, ModbusServerPoint> _byTagId;
    private readonly ModbusServerRegisterMap _map;
    private readonly ModbusServerProtocolHandler _handler;
    private readonly object _diagnosticsGate = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private HostSerialBusCoordinator.HostSerialBusLease? _lease;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _readRequests;
    private long _writeRequests;
    private long _rejectedWrites;
    private long _outOfRangeRequests;
    private long _crcErrors;
    private long _protocolExceptions;
    private long _updatesPublished;
    private long _operationCount;
    private long _failedOperations;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private CommunicationDriverReadinessState _readinessState = CommunicationDriverReadinessState.NotStarted;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;

    public ModbusRtuServerDriver(
        string driverId,
        string name,
        HostSerialBusCoordinator coordinator,
        HostSerialLineSettings serialSettings,
        byte unitId,
        IEnumerable<ModbusHoldingRegisterRange> ranges,
        IEnumerable<ModbusServerPoint> points,
        ICurrentTagCache cache,
        ITagRegistry registry,
        TimeSpan? frameTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        if (unitId is < 1 or > 247) throw new ArgumentOutOfRangeException(nameof(unitId));
        DriverId = driverId.Trim();
        Name = name.Trim();
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _serialSettings = serialSettings ?? throw new ArgumentNullException(nameof(serialSettings));
        _serialSettings.Validate();
        _frameTimeout = frameTimeout ?? TimeSpan.FromSeconds(1);
        if (_frameTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frameTimeout));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _points = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one Modbus Server TAG is required.", nameof(points));
        foreach (var point in _points)
        {
            point.Validate();
            if (point.Point.UnitId != unitId)
                throw new ArgumentException($"TAG '{point.Point.Tag.Path}' Unit ID must match server Unit ID {unitId}.", nameof(points));
        }

        _byTagId = _points.ToDictionary(point => point.Point.Tag.Id);
        _map = new ModbusServerRegisterMap(ranges, _points);
        _handler = new ModbusServerProtocolHandler(unitId, _map, PublishExternalUpdatesAsync);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Point.Tag).ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        SetState(CommunicationDriverOperationalState.Starting, CommunicationDriverReadinessState.Starting);

        foreach (var point in _points)
            if (!_registry.TryGet(point.Point.Tag.Id, out _))
                _registry.Register(point.Point.Tag);

        try
        {
            _lease = await _coordinator.AcquireServerAsync(DriverId, _serialSettings, cancellationToken);
        }
        catch (Exception ex)
        {
            RecordFailure(ex.Message);
            SetState(CommunicationDriverOperationalState.Faulted, CommunicationDriverReadinessState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, Sanitize(ex.Message));
            throw;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        SetState(CommunicationDriverOperationalState.Healthy, CommunicationDriverReadinessState.Ready);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null && _lease is null) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        SetState(CommunicationDriverOperationalState.Stopping, CommunicationDriverReadinessState.Stopped);
        if (_cts is not null) await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true) { }
        }
        var lease = Interlocked.Exchange(ref _lease, null);
        if (lease is not null) await lease.DisposeAsync();
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        SetState(CommunicationDriverOperationalState.Stopped, CommunicationDriverReadinessState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"Modbus RTU Server TAG '{tagId}' was not found.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_byTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Modbus RTU Server TAG '{tagId}' was not found.");
        if (point.Point.Tag.ReadOnly)
            throw new InvalidOperationException($"Modbus RTU Server TAG '{point.Point.Tag.Path}' is read-only to internal writes.");

        var update = await _map.WriteInternalAsync(tagId, value, cancellationToken);
        await PublishAsync(update, cancellationToken);
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness() =>
        new(
            DriverId,
            ModbusRtuServerDriverDescriptorProvider.DriverTypeId,
            _readinessState,
            DateTimeOffset.UtcNow,
            _readinessState == CommunicationDriverReadinessState.Ready
                ? $"Serial server open on {_serialSettings.PortName}."
                : _lastError,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["serialPort"] = _serialSettings.PortName,
                ["unitId"] = _handler.UnitId.ToString(CultureInfo.InvariantCulture)
            });

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        lock (_diagnosticsGate)
        {
            var average = _operationCount == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationDurationTicks / _operationCount);
            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                ModbusRtuServerDriverDescriptorProvider.DriverTypeId,
                _runtimeInstanceId,
                _serialSettings.PortName,
                _communicationState,
                _stateChangedAt,
                DateTimeOffset.UtcNow,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                null,
                null,
                _operationCount == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                average,
                null,
                _operationCount == 0 ? 0d : _failedOperations / (double)_operationCount,
                _points.Count,
                BuildQualitySummary(),
                new CommunicationDriverCounters(
                    0,
                    _readRequests + _writeRequests + _protocolExceptions,
                    _operationCount - _failedOperations,
                    _failedOperations,
                    0,
                    0,
                    _lease is null ? 0 : 1,
                    0,
                    0,
                    _readRequests,
                    _writeRequests,
                    _updatesPublished),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["serialPort"] = _serialSettings.PortName,
                    ["baudRate"] = _serialSettings.BaudRate.ToString(CultureInfo.InvariantCulture),
                    ["dataBits"] = _serialSettings.DataBits.ToString(CultureInfo.InvariantCulture),
                    ["parity"] = _serialSettings.Parity.ToString(),
                    ["stopBits"] = _serialSettings.StopBits.ToString(),
                    ["open"] = (_lease is not null).ToString(),
                    ["readRequests"] = _readRequests.ToString(CultureInfo.InvariantCulture),
                    ["writeRequests"] = _writeRequests.ToString(CultureInfo.InvariantCulture),
                    ["rejectedWrites"] = _rejectedWrites.ToString(CultureInfo.InvariantCulture),
                    ["outOfRangeRequests"] = _outOfRangeRequests.ToString(CultureInfo.InvariantCulture),
                    ["crcErrors"] = _crcErrors.ToString(CultureInfo.InvariantCulture),
                    ["protocolExceptions"] = _protocolExceptions.ToString(CultureInfo.InvariantCulture),
                    ["registerCount"] = _map.RegisterCount.ToString(CultureInfo.InvariantCulture),
                    ["tagCount"] = _map.TagCount.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lease = _lease;
                if (lease is null) break;
                await lease.ExecuteSerializedAsync(
                    async (connection, token) =>
                    {
                        var frame = await ReadRequestFrameAsync(connection, token);
                        if (frame is null) return;
                        if (!ModbusRtuCrc.IsValid(frame))
                        {
                            lock (_diagnosticsGate)
                            {
                                _crcErrors++;
                                _failedOperations++;
                                _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
                                _lastError = "Received Modbus RTU frame with invalid CRC.";
                            }
                            return;
                        }

                        var unit = frame[0];
                        if (unit != _handler.UnitId)
                            return;

                        var pdu = frame.AsMemory(1, frame.Length - 3);
                        var started = Stopwatch.GetTimestamp();
                        var result = await _handler.HandleAsync(unit, pdu, token);
                        RecordProtocolResult(result, Stopwatch.GetElapsedTime(started));
                        var response = ModbusRtuCrc.Frame(unit, result.ResponsePdu);
                        await connection.WriteAsync(response, token);
                        await connection.FlushAsync(token);
                    },
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            RecordFailure(ex.Message);
            SetState(CommunicationDriverOperationalState.Faulted, CommunicationDriverReadinessState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, Sanitize(ex.Message), _updatesPublished);
        }
    }

    private async ValueTask<byte[]?> ReadRequestFrameAsync(
        IHostSerialConnection connection,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[2];
        var first = await connection.ReadAsync(prefix.AsMemory(0, 1), cancellationToken);
        if (first <= 0) return null;

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(_frameTimeout);
        await ReadExactlyAsync(connection, prefix.AsMemory(1, 1), bounded.Token);

        var function = prefix[1];
        if (function == ModbusPduCodec.WriteMultipleRegisters)
        {
            var headerTail = new byte[5];
            await ReadExactlyAsync(connection, headerTail, bounded.Token);
            var byteCount = headerTail[4];
            if (byteCount > 246) throw new IOException("Modbus RTU FC16 byte count exceeds protocol bounds.");
            var frame = new byte[2 + headerTail.Length + byteCount + 2];
            prefix.CopyTo(frame, 0);
            headerTail.CopyTo(frame, 2);
            await ReadExactlyAsync(connection, frame.AsMemory(7), bounded.Token);
            return frame;
        }

        var fixedFrame = new byte[8];
        prefix.CopyTo(fixedFrame, 0);
        await ReadExactlyAsync(connection, fixedFrame.AsMemory(2), bounded.Token);
        return fixedFrame;
    }

    private static async ValueTask ReadExactlyAsync(
        IHostSerialConnection connection,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[offset..], cancellationToken);
            if (read <= 0) throw new EndOfStreamException("Serial stream ended before the Modbus RTU request was complete.");
            offset += read;
        }
    }

    private void RecordProtocolResult(ModbusServerProtocolResult result, TimeSpan duration)
    {
        lock (_diagnosticsGate)
        {
            _operationCount++;
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            if (result.Kind == ModbusServerOperationKind.Read) _readRequests++;
            else if (result.Kind == ModbusServerOperationKind.Write) _writeRequests++;
            else
            {
                _failedOperations++;
                _protocolExceptions++;
                _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
                if (result.Failure == ModbusServerRequestFailure.ReadOnly) _rejectedWrites++;
                if (result.Failure == ModbusServerRequestFailure.IllegalAddress) _outOfRangeRequests++;
            }
            if (result.Kind != ModbusServerOperationKind.Rejected)
                _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
        }
    }

    private void RecordFailure(string message)
    {
        lock (_diagnosticsGate)
        {
            _failedOperations++;
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = Sanitize(message);
        }
    }

    private async ValueTask PublishExternalUpdatesAsync(
        IReadOnlyCollection<ModbusServerTagUpdate> updates,
        CancellationToken cancellationToken)
    {
        foreach (var update in updates)
            await PublishAsync(update, cancellationToken);
    }

    private async ValueTask PublishAsync(ModbusServerTagUpdate update, CancellationToken cancellationToken)
    {
        var sample = new TagValue(update.Tag.Id, update.EngineeringValue, DateTimeOffset.UtcNow, TagQuality.Good, DriverId);
        await _cache.UpdateAsync(update.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
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
            if (!_cache.TryGet(point.Point.Tag.Id, out var sample) || sample is null) { noSample++; continue; }
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
        return new CommunicationTagQualitySummary(good, badCommunication, uncertain, bad, badConfiguration, badDevice, stale, disabled, noSample);
    }

    private void SetState(CommunicationDriverOperationalState state, CommunicationDriverReadinessState readiness)
    {
        lock (_diagnosticsGate)
        {
            _communicationState = state;
            _readinessState = readiness;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
