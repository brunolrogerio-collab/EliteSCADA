using System.Diagnostics;
using System.Globalization;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Modbus;

public sealed class ModbusRtuCrcException : IOException
{
    public ModbusRtuCrcException(string message) : base(message) { }
}

/// <summary>
/// Native Modbus RTU master framing over one host-owned serial bus lease.
/// The bus coordinator owns physical-port sharing and serializes all transactions.
/// </summary>
public sealed class ModbusRtuTransport : IModbusMasterTransport
{
    private readonly HostSerialBusCoordinator _coordinator;
    private readonly HostSerialLineSettings _settings;
    private readonly string _ownerId;
    private readonly IReadOnlyCollection<byte> _unitIds;
    private readonly bool _engineeringLease;
    private readonly object _diagnosticsGate = new();
    private readonly SemaphoreSlim _leaseGate = new(1, 1);
    private HostSerialBusCoordinator.HostSerialBusLease? _lease;
    private long _connectionCount;
    private long _disconnectionCount;
    private long _requestAttempts;
    private long _successfulRequestAttempts;
    private long _failedRequestAttempts;
    private long _timeoutCount;
    private long _crcErrorCount;
    private long _protocolExceptionCount;
    private long _lastRequestDurationTicks;
    private long _totalRequestDurationTicks;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;

    public ModbusRtuTransport(
        HostSerialBusCoordinator coordinator,
        HostSerialLineSettings settings,
        string ownerId,
        IReadOnlyCollection<byte> unitIds,
        TimeSpan? requestTimeout = null,
        bool engineeringLease = false)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("RTU serial owner ID is required.", nameof(ownerId));
        _ownerId = ownerId.Trim();
        _unitIds = (unitIds ?? throw new ArgumentNullException(nameof(unitIds))).Distinct().OrderBy(x => x).ToArray();
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        _engineeringLease = engineeringLease;
    }

    public string Endpoint => _settings.PortName;
    public TimeSpan RequestTimeout { get; }
    public bool IsConnected => _lease is not null;
    public IReadOnlyDictionary<string, string> ProtocolDetails => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["transport"] = "rtu",
        ["serialPort"] = _settings.PortName,
        ["baudRate"] = _settings.BaudRate.ToString(CultureInfo.InvariantCulture),
        ["dataBits"] = _settings.DataBits.ToString(CultureInfo.InvariantCulture),
        ["parity"] = _settings.Parity.ToString(),
        ["stopBits"] = _settings.StopBits.ToString(),
        ["sharedBusIdentity"] = _settings.PhysicalPortKey,
        ["requestTimeoutMs"] = RequestTimeout.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
    };

    public ModbusMasterTransportDiagnosticSnapshot GetMasterDiagnostics()
    {
        lock (_diagnosticsGate)
        {
            var average = _requestAttempts == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalRequestDurationTicks / _requestAttempts);
            return new ModbusMasterTransportDiagnosticSnapshot(
                Endpoint,
                RequestTimeout,
                IsConnected,
                _connectionCount,
                _disconnectionCount,
                Math.Max(0, _connectionCount - 1),
                _requestAttempts,
                _successfulRequestAttempts,
                _failedRequestAttempts,
                _timeoutCount,
                _crcErrorCount,
                _protocolExceptionCount,
                _requestAttempts == 0 ? null : TimeSpan.FromTicks(_lastRequestDurationTicks),
                average,
                _lastConnectedAt,
                _lastDisconnectedAt,
                ProtocolDetails);
        }
    }

    public async Task<bool[]> ReadBitsAsync(byte unitId, ModbusDataArea area, ushort address, ushort quantity, CancellationToken cancellationToken = default)
    {
        if (area is not (ModbusDataArea.Coil or ModbusDataArea.DiscreteInput))
            throw new ArgumentException("Bit reads require Coil or DiscreteInput area.", nameof(area));
        var function = area == ModbusDataArea.Coil ? ModbusPduCodec.ReadCoils : ModbusPduCodec.ReadDiscreteInputs;
        var response = await SendRequestAsync(unitId, ModbusPduCodec.BuildReadRequest(function, address, quantity), cancellationToken);
        return ModbusPduCodec.DecodeBitReadResponse(response, function, quantity);
    }

    public async Task<ushort[]> ReadRegistersAsync(byte unitId, ModbusDataArea area, ushort address, ushort quantity, CancellationToken cancellationToken = default)
    {
        if (area is not (ModbusDataArea.HoldingRegister or ModbusDataArea.InputRegister))
            throw new ArgumentException("Register reads require HoldingRegister or InputRegister area.", nameof(area));
        var function = area == ModbusDataArea.HoldingRegister ? ModbusPduCodec.ReadHoldingRegisters : ModbusPduCodec.ReadInputRegisters;
        var response = await SendRequestAsync(unitId, ModbusPduCodec.BuildReadRequest(function, address, quantity), cancellationToken);
        return ModbusPduCodec.DecodeRegisterReadResponse(response, function, quantity);
    }

    public async Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken cancellationToken = default)
    {
        var request = ModbusPduCodec.BuildWriteSingleCoilRequest(address, value);
        var response = await SendRequestAsync(unitId, request, cancellationToken);
        ModbusPduCodec.ValidateWriteEchoResponse(response, request, ModbusPduCodec.WriteSingleCoil);
    }

    public async Task WriteSingleRegisterAsync(byte unitId, ushort address, ushort value, CancellationToken cancellationToken = default)
    {
        var request = ModbusPduCodec.BuildWriteSingleRegisterRequest(address, value);
        var response = await SendRequestAsync(unitId, request, cancellationToken);
        ModbusPduCodec.ValidateWriteEchoResponse(response, request, ModbusPduCodec.WriteSingleRegister);
    }

    public async Task WriteMultipleRegistersAsync(byte unitId, ushort address, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        var request = ModbusPduCodec.BuildWriteMultipleRegistersRequest(address, values);
        var response = await SendRequestAsync(unitId, request, cancellationToken);
        ModbusPduCodec.ValidateWriteMultipleResponse(response, address, checked((ushort)values.Count));
    }

    private async Task<byte[]> SendRequestAsync(byte unitId, byte[] pdu, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var lease = await EnsureLeaseAsync(cancellationToken);
            var response = await lease.ExecuteSerializedAsync(
                async (connection, token) =>
                {
                    connection.DiscardInput();
                    var requestFrame = ModbusRtuCrc.Frame(unitId, pdu);
                    await connection.WriteAsync(requestFrame, token);
                    await connection.FlushAsync(token);

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(RequestTimeout);
                    var responseFrame = await ReadResponseFrameAsync(connection, timeout.Token);
                    if (!ModbusRtuCrc.IsValid(responseFrame))
                        throw new ModbusRtuCrcException("Modbus RTU response CRC is invalid.");
                    if (responseFrame[0] != unitId)
                        throw new IOException($"Modbus RTU unit identifier mismatch. Expected {unitId}, received {responseFrame[0]}.");

                    var responsePdu = responseFrame.AsSpan(1, responseFrame.Length - 3).ToArray();
                    ModbusPduCodec.ThrowIfException(responsePdu);
                    return responsePdu;
                },
                cancellationToken);

            RecordRequest(true, false, null, Stopwatch.GetElapsedTime(started));
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RecordRequest(false, true, null, Stopwatch.GetElapsedTime(started));
            throw new TimeoutException($"Modbus RTU request on '{_settings.PortName}' timed out after {RequestTimeout}.");
        }
        catch (ModbusRtuCrcException ex)
        {
            RecordRequest(false, false, ex, Stopwatch.GetElapsedTime(started));
            throw;
        }
        catch (ModbusProtocolException ex)
        {
            RecordRequest(false, false, ex, Stopwatch.GetElapsedTime(started));
            throw;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            RecordRequest(false, false, ex, Stopwatch.GetElapsedTime(started));
            throw;
        }
    }

    private async ValueTask<HostSerialBusCoordinator.HostSerialBusLease> EnsureLeaseAsync(CancellationToken cancellationToken)
    {
        if (_lease is not null) return _lease;
        await _leaseGate.WaitAsync(cancellationToken);
        try
        {
            if (_lease is not null) return _lease;
            _lease = _engineeringLease
                ? await _coordinator.AcquireEngineeringMasterAsync(_ownerId, _settings, cancellationToken)
                : await _coordinator.AcquireMasterAsync(_ownerId, _settings, _unitIds, cancellationToken);
            lock (_diagnosticsGate)
            {
                _connectionCount++;
                _lastConnectedAt = DateTimeOffset.UtcNow;
            }
            return _lease;
        }
        finally
        {
            _leaseGate.Release();
        }
    }

    private static async ValueTask<byte[]> ReadResponseFrameAsync(IHostSerialConnection connection, CancellationToken cancellationToken)
    {
        var header = new byte[3];
        await ReadExactlyAsync(connection, header, cancellationToken);
        var function = header[1];
        int remaining;
        if ((function & 0x80) != 0)
            remaining = 2;
        else if (function is ModbusPduCodec.ReadCoils or ModbusPduCodec.ReadDiscreteInputs or ModbusPduCodec.ReadHoldingRegisters or ModbusPduCodec.ReadInputRegisters)
            remaining = header[2] + 2;
        else if (function is ModbusPduCodec.WriteSingleCoil or ModbusPduCodec.WriteSingleRegister or ModbusPduCodec.WriteMultipleRegisters)
            remaining = 5;
        else
            throw new IOException($"Unsupported Modbus RTU response function 0x{function:X2}.");

        if (remaining < 2 || remaining > 253)
            throw new IOException("Modbus RTU response length is invalid.");

        var frame = new byte[header.Length + remaining];
        header.CopyTo(frame, 0);
        await ReadExactlyAsync(connection, frame.AsMemory(header.Length), cancellationToken);
        return frame;
    }

    private static async ValueTask ReadExactlyAsync(IHostSerialConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[offset..], cancellationToken);
            if (read <= 0) throw new EndOfStreamException("Serial stream ended before the Modbus RTU frame was complete.");
            offset += read;
        }
    }

    private void RecordRequest(bool success, bool timeout, Exception? error, TimeSpan duration)
    {
        lock (_diagnosticsGate)
        {
            _requestAttempts++;
            if (success) _successfulRequestAttempts++; else _failedRequestAttempts++;
            if (timeout) _timeoutCount++;
            if (error is ModbusRtuCrcException) _crcErrorCount++;
            if (error is ModbusProtocolException) _protocolExceptionCount++;
            _lastRequestDurationTicks = duration.Ticks;
            _totalRequestDurationTicks += duration.Ticks;
        }
    }

    public async Task DisconnectAsync()
    {
        var lease = Interlocked.Exchange(ref _lease, null);
        if (lease is null) return;
        await lease.DisposeAsync();
        lock (_diagnosticsGate)
        {
            _disconnectionCount++;
            _lastDisconnectedAt = DateTimeOffset.UtcNow;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _leaseGate.Dispose();
    }
}
