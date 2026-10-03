using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpTransport : IAsyncDisposable
{
    private const ushort ProtocolId = 0;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _requestTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private TcpClient? _client;
    private int _transactionId;
    private long _connectionCount;
    private long _disconnectionCount;
    private long _reconnectCount;
    private long _requestAttempts;
    private long _successfulRequestAttempts;
    private long _failedRequestAttempts;
    private long _timeoutCount;
    private long _lastRequestDurationTicks;
    private long _totalRequestDurationTicks;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;

    public ModbusTcpTransport(string host, int port = 502, TimeSpan? requestTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Modbus TCP host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _host = host.Trim();
        _port = port;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public string Host => _host;
    public int Port => _port;
    public TimeSpan RequestTimeout => _requestTimeout;
    public bool IsConnected => _client?.Connected == true;

    public ModbusTcpTransportDiagnosticSnapshot GetDiagnostics()
    {
        var connected = IsConnected;
        lock (_diagnosticsGate)
        {
            var average = _requestAttempts == 0
                ? (TimeSpan?)null
                : TimeSpan.FromTicks(_totalRequestDurationTicks / _requestAttempts);
            return new ModbusTcpTransportDiagnosticSnapshot(
                _host,
                _port,
                _requestTimeout,
                connected,
                _connectionCount,
                _disconnectionCount,
                _reconnectCount,
                _requestAttempts,
                _successfulRequestAttempts,
                _failedRequestAttempts,
                _timeoutCount,
                _requestAttempts == 0 ? null : TimeSpan.FromTicks(_lastRequestDurationTicks),
                average,
                _lastConnectedAt,
                _lastDisconnectedAt);
        }
    }

    public async Task<bool[]> ReadBitsAsync(
        byte unitId,
        ModbusDataArea area,
        ushort address,
        ushort quantity,
        CancellationToken cancellationToken = default)
    {
        if (area is not (ModbusDataArea.Coil or ModbusDataArea.DiscreteInput))
            throw new ArgumentException("Bit reads require Coil or DiscreteInput area.", nameof(area));
        if (quantity is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(quantity));

        var function = area == ModbusDataArea.Coil ? ModbusPduCodec.ReadCoils : ModbusPduCodec.ReadDiscreteInputs;
        var response = await SendRequestAsync(
            unitId,
            ModbusPduCodec.BuildReadRequest(function, address, quantity),
            retryOnConnectionFailure: true,
            cancellationToken);
        return ModbusPduCodec.DecodeBitReadResponse(response, function, quantity);
    }

    public async Task<ushort[]> ReadRegistersAsync(
        byte unitId,
        ModbusDataArea area,
        ushort address,
        ushort quantity,
        CancellationToken cancellationToken = default)
    {
        if (area is not (ModbusDataArea.HoldingRegister or ModbusDataArea.InputRegister))
            throw new ArgumentException("Register reads require HoldingRegister or InputRegister area.", nameof(area));
        if (quantity is < 1 or > 125) throw new ArgumentOutOfRangeException(nameof(quantity));

        var function = area == ModbusDataArea.HoldingRegister ? ModbusPduCodec.ReadHoldingRegisters : ModbusPduCodec.ReadInputRegisters;
        var response = await SendRequestAsync(
            unitId,
            ModbusPduCodec.BuildReadRequest(function, address, quantity),
            retryOnConnectionFailure: true,
            cancellationToken);
        return ModbusPduCodec.DecodeRegisterReadResponse(response, function, quantity);
    }

    public async Task WriteSingleCoilAsync(
        byte unitId,
        ushort address,
        bool value,
        CancellationToken cancellationToken = default)
    {
        var pdu = ModbusPduCodec.BuildWriteSingleCoilRequest(address, value);
        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ModbusPduCodec.ValidateWriteEchoResponse(response, pdu, ModbusPduCodec.WriteSingleCoil);
    }

    public async Task WriteSingleRegisterAsync(
        byte unitId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        var pdu = ModbusPduCodec.BuildWriteSingleRegisterRequest(address, value);
        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ModbusPduCodec.ValidateWriteEchoResponse(response, pdu, ModbusPduCodec.WriteSingleRegister);
    }

    public async Task WriteMultipleRegistersAsync(
        byte unitId,
        ushort address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values));

        var pdu = ModbusPduCodec.BuildWriteMultipleRegistersRequest(address, values);
        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ModbusPduCodec.ValidateWriteMultipleResponse(response, address, checked((ushort)values.Count));
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try { ResetConnection(); }
        finally { _gate.Release(); }
    }

    private async Task<byte[]> SendRequestAsync(
        byte unitId,
        byte[] pdu,
        bool retryOnConnectionFailure,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var attempts = retryOnConnectionFailure ? 2 : 1;
            Exception? last = null;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(_requestTimeout);
                    var response = await SendOnceAsync(unitId, pdu, timeout.Token);
                    RecordRequest(success: true, timeout: false, Stopwatch.GetElapsedTime(started));
                    return response;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    RecordRequest(success: false, timeout: true, Stopwatch.GetElapsedTime(started));
                    ResetConnection();
                    last = new TimeoutException($"Modbus TCP request to {_host}:{_port} timed out after {_requestTimeout}.");
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                    RecordRequest(success: false, timeout: false, Stopwatch.GetElapsedTime(started));
                    ResetConnection();
                    last = ex;
                }
            }

            throw last ?? new IOException("Modbus TCP request failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<byte[]> SendOnceAsync(byte unitId, byte[] pdu, CancellationToken cancellationToken)
    {
        var client = await EnsureConnectedAsync(cancellationToken);
        var stream = client.GetStream();
        var transactionId = unchecked((ushort)Interlocked.Increment(ref _transactionId));
        var request = new byte[7 + pdu.Length];
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), ProtocolId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), checked((ushort)(pdu.Length + 1)));
        request[6] = unitId;
        pdu.CopyTo(request, 7);

        await stream.WriteAsync(request, cancellationToken);

        var header = new byte[7];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var responseTransaction = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        var responseProtocol = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        if (responseTransaction != transactionId) throw new IOException("Modbus TCP transaction identifier mismatch.");
        if (responseProtocol != ProtocolId) throw new IOException("Modbus TCP protocol identifier is not zero.");
        if (header[6] != unitId) throw new IOException("Modbus TCP unit identifier mismatch.");
        if (length is < 2 or > 254) throw new IOException("Modbus TCP response length is invalid.");

        var response = new byte[length - 1];
        await stream.ReadExactlyAsync(response, cancellationToken);
        ModbusPduCodec.ThrowIfException(response);
        return response;
    }

    private async Task<TcpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client?.Connected == true) return _client;
        ResetConnection();
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(_host, _port, cancellationToken);
            _client = client;
            RecordConnected();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private void RecordRequest(bool success, bool timeout, TimeSpan duration)
    {
        lock (_diagnosticsGate)
        {
            _requestAttempts++;
            if (success) _successfulRequestAttempts++; else _failedRequestAttempts++;
            if (timeout) _timeoutCount++;
            _lastRequestDurationTicks = duration.Ticks;
            _totalRequestDurationTicks += duration.Ticks;
        }
    }

    private void RecordConnected()
    {
        lock (_diagnosticsGate)
        {
            _connectionCount++;
            if (_connectionCount > 1) _reconnectCount++;
            _lastConnectedAt = DateTimeOffset.UtcNow;
        }
    }

    private void RecordDisconnected()
    {
        lock (_diagnosticsGate)
        {
            _disconnectionCount++;
            _lastDisconnectedAt = DateTimeOffset.UtcNow;
        }
    }

    private void ResetConnection()
    {
        var client = _client;
        _client = null;
        if (client is null) return;
        try { client.Dispose(); } catch { }
        RecordDisconnected();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _gate.Dispose();
    }
}

public sealed class ModbusProtocolException : IOException
{
    public ModbusProtocolException(byte functionCode, byte exceptionCode)
        : base($"Modbus function 0x{functionCode:X2} returned exception code 0x{exceptionCode:X2}.")
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }

    public byte FunctionCode { get; }
    public byte ExceptionCode { get; }
}
