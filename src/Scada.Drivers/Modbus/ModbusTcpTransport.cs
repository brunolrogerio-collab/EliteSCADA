using System.Buffers.Binary;
using System.Net.Sockets;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpTransport : IAsyncDisposable
{
    private const ushort ProtocolId = 0;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _requestTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private int _transactionId;

    public ModbusTcpTransport(string host, int port = 502, TimeSpan? requestTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Modbus TCP host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _host = host.Trim();
        _port = port;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public bool IsConnected => _client?.Connected == true;

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

        var function = area == ModbusDataArea.Coil ? (byte)0x01 : (byte)0x02;
        var response = await SendRequestAsync(unitId, BuildReadPdu(function, address, quantity), retryOnConnectionFailure: true, cancellationToken);
        ValidateFunction(response, function);
        if (response.Length < 2) throw new IOException("Modbus bit response is truncated.");
        var byteCount = response[1];
        var expectedBytes = (quantity + 7) / 8;
        if (byteCount != expectedBytes || response.Length != byteCount + 2)
            throw new IOException("Modbus bit response byte count is invalid.");

        var result = new bool[quantity];
        for (var i = 0; i < result.Length; i++)
            result[i] = (response[2 + i / 8] & (1 << (i % 8))) != 0;
        return result;
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

        var function = area == ModbusDataArea.HoldingRegister ? (byte)0x03 : (byte)0x04;
        var response = await SendRequestAsync(unitId, BuildReadPdu(function, address, quantity), retryOnConnectionFailure: true, cancellationToken);
        ValidateFunction(response, function);
        if (response.Length < 2) throw new IOException("Modbus register response is truncated.");
        var byteCount = response[1];
        if (byteCount != quantity * 2 || response.Length != byteCount + 2)
            throw new IOException("Modbus register response byte count is invalid.");

        var result = new ushort[quantity];
        for (var i = 0; i < result.Length; i++)
            result[i] = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2 + i * 2, 2));
        return result;
    }

    public async Task WriteSingleCoilAsync(
        byte unitId,
        ushort address,
        bool value,
        CancellationToken cancellationToken = default)
    {
        var pdu = new byte[5];
        pdu[0] = 0x05;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value ? (ushort)0xFF00 : (ushort)0x0000);
        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ValidateWriteEcho(response, pdu, 0x05);
    }

    public async Task WriteSingleRegisterAsync(
        byte unitId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        var pdu = new byte[5];
        pdu[0] = 0x06;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);
        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ValidateWriteEcho(response, pdu, 0x06);
    }

    public async Task WriteMultipleRegistersAsync(
        byte unitId,
        ushort address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values));

        var pdu = new byte[6 + values.Count * 2];
        pdu[0] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), checked((ushort)values.Count));
        pdu[5] = checked((byte)(values.Count * 2));
        for (var i = 0; i < values.Count; i++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + i * 2, 2), values[i]);

        var response = await SendRequestAsync(unitId, pdu, retryOnConnectionFailure: false, cancellationToken);
        ValidateFunction(response, 0x10);
        if (response.Length != 5 ||
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(1, 2)) != address ||
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(3, 2)) != values.Count)
            throw new IOException("Modbus FC16 response does not match the request.");
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
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(_requestTimeout);
                    return await SendOnceAsync(unitId, pdu, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    ResetConnection();
                    last = new TimeoutException($"Modbus TCP request to {_host}:{_port} timed out after {_requestTimeout}.");
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
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
        if ((response[0] & 0x80) != 0)
        {
            var exceptionCode = response.Length > 1 ? response[1] : (byte)0;
            throw new ModbusProtocolException((byte)(response[0] & 0x7F), exceptionCode);
        }
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
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static byte[] BuildReadPdu(byte function, ushort address, ushort quantity)
    {
        var pdu = new byte[5];
        pdu[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        return pdu;
    }

    private static void ValidateFunction(byte[] response, byte expectedFunction)
    {
        if (response.Length == 0 || response[0] != expectedFunction)
            throw new IOException($"Unexpected Modbus function code in response. Expected 0x{expectedFunction:X2}.");
    }

    private static void ValidateWriteEcho(byte[] response, byte[] requestPdu, byte function)
    {
        ValidateFunction(response, function);
        if (response.Length != requestPdu.Length || !response.AsSpan().SequenceEqual(requestPdu))
            throw new IOException($"Modbus FC{function:X2} response does not match the request.");
    }

    private void ResetConnection()
    {
        try { _client?.Dispose(); } catch { }
        _client = null;
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
