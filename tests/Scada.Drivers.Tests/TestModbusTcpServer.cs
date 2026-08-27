using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Scada.Drivers.Tests;

internal sealed class TestModbusTcpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
    private readonly List<ModbusRequestRecord> _requests = new();
    private readonly object _requestsGate = new();
    private Task? _acceptLoop;
    private int _clientId;

    public ConcurrentDictionary<ushort, bool> Coils { get; } = new();
    public ConcurrentDictionary<ushort, bool> DiscreteInputs { get; } = new();
    public ConcurrentDictionary<ushort, ushort> HoldingRegisters { get; } = new();
    public ConcurrentDictionary<ushort, ushort> InputRegisters { get; } = new();
    public bool RejectWrites { get; set; }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public IReadOnlyList<ModbusRequestRecord> Requests
    {
        get
        {
            lock (_requestsGate) return _requests.ToArray();
        }
    }

    public void Start()
    {
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void DropConnections()
    {
        foreach (var client in _clients.Values)
        {
            try { client.Dispose(); } catch { }
        }
        _clients.Clear();
    }

    public async Task StopAsync()
    {
        if (_cts.IsCancellationRequested) return;
        await _cts.CancelAsync();
        _listener.Stop();
        DropConnections();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_cts.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested) { }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var id = Interlocked.Increment(ref _clientId);
            _clients[id] = client;
            _ = Task.Run(async () =>
            {
                try { await HandleClientAsync(client, cancellationToken); }
                finally
                {
                    _clients.TryRemove(id, out _);
                    client.Dispose();
                }
            }, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        var stream = client.GetStream();
        while (!cancellationToken.IsCancellationRequested)
        {
            var header = new byte[7];
            try
            {
                await stream.ReadExactlyAsync(header, cancellationToken);
            }
            catch (EndOfStreamException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var transactionId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
            var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            var unitId = header[6];
            if (protocolId != 0 || length < 2) return;

            var pdu = new byte[length - 1];
            await stream.ReadExactlyAsync(pdu, cancellationToken);
            var responsePdu = HandlePdu(unitId, pdu);

            var response = new byte[7 + responsePdu.Length];
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), transactionId);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), checked((ushort)(responsePdu.Length + 1)));
            response[6] = unitId;
            responsePdu.CopyTo(response, 7);
            await stream.WriteAsync(response, cancellationToken);
        }
    }

    private byte[] HandlePdu(byte unitId, byte[] pdu)
    {
        if (pdu.Length == 0) return new byte[] { 0x80, 0x03 };
        var function = pdu[0];
        try
        {
            return function switch
            {
                0x01 => ReadBits(unitId, function, pdu, Coils),
                0x02 => ReadBits(unitId, function, pdu, DiscreteInputs),
                0x03 => ReadRegisters(unitId, function, pdu, HoldingRegisters),
                0x04 => ReadRegisters(unitId, function, pdu, InputRegisters),
                0x05 => RejectWrites ? RejectWrite(unitId, function, pdu) : WriteSingleCoil(unitId, pdu),
                0x06 => RejectWrites ? RejectWrite(unitId, function, pdu) : WriteSingleRegister(unitId, pdu),
                0x10 => RejectWrites ? RejectWrite(unitId, function, pdu) : WriteMultipleRegisters(unitId, pdu),
                _ => new byte[] { (byte)(function | 0x80), 0x01 }
            };
        }
        catch
        {
            return new byte[] { (byte)(function | 0x80), 0x03 };
        }
    }

    private byte[] RejectWrite(byte unitId, byte function, byte[] pdu)
    {
        var address = pdu.Length >= 3 ? BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)) : (ushort)0;
        var quantity = function == 0x10 && pdu.Length >= 5
            ? BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2))
            : (ushort)1;
        Record(unitId, function, address, quantity);
        return new byte[] { (byte)(function | 0x80), 0x04 };
    }

    private byte[] ReadBits(byte unitId, byte function, byte[] pdu, ConcurrentDictionary<ushort, bool> source)
    {
        EnsureLength(pdu, 5);
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
        Record(unitId, function, address, quantity);
        var byteCount = (quantity + 7) / 8;
        var response = new byte[2 + byteCount];
        response[0] = function;
        response[1] = checked((byte)byteCount);
        for (var i = 0; i < quantity; i++)
        {
            if (source.TryGetValue(checked((ushort)(address + i)), out var value) && value)
                response[2 + i / 8] |= checked((byte)(1 << (i % 8)));
        }
        return response;
    }

    private byte[] ReadRegisters(byte unitId, byte function, byte[] pdu, ConcurrentDictionary<ushort, ushort> source)
    {
        EnsureLength(pdu, 5);
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
        Record(unitId, function, address, quantity);
        var response = new byte[2 + quantity * 2];
        response[0] = function;
        response[1] = checked((byte)(quantity * 2));
        for (var i = 0; i < quantity; i++)
        {
            source.TryGetValue(checked((ushort)(address + i)), out var value);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + i * 2, 2), value);
        }
        return response;
    }

    private byte[] WriteSingleCoil(byte unitId, byte[] pdu)
    {
        EnsureLength(pdu, 5);
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
        var encoded = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
        if (encoded is not (0x0000 or 0xFF00)) throw new InvalidDataException();
        Coils[address] = encoded == 0xFF00;
        Record(unitId, 0x05, address, 1);
        return pdu.ToArray();
    }

    private byte[] WriteSingleRegister(byte unitId, byte[] pdu)
    {
        EnsureLength(pdu, 5);
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
        var value = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
        HoldingRegisters[address] = value;
        Record(unitId, 0x06, address, 1);
        return pdu.ToArray();
    }

    private byte[] WriteMultipleRegisters(byte unitId, byte[] pdu)
    {
        if (pdu.Length < 6) throw new InvalidDataException();
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
        var byteCount = pdu[5];
        if (byteCount != quantity * 2 || pdu.Length != 6 + byteCount) throw new InvalidDataException();
        for (var i = 0; i < quantity; i++)
            HoldingRegisters[checked((ushort)(address + i))] = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(6 + i * 2, 2));
        Record(unitId, 0x10, address, quantity);

        var response = new byte[5];
        response[0] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(3, 2), quantity);
        return response;
    }

    private void Record(byte unitId, byte function, ushort address, ushort quantity)
    {
        lock (_requestsGate) _requests.Add(new ModbusRequestRecord(unitId, function, address, quantity));
    }

    private static void EnsureLength(byte[] pdu, int expected)
    {
        if (pdu.Length != expected) throw new InvalidDataException();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}

internal sealed record ModbusRequestRecord(byte UnitId, byte Function, ushort Address, ushort Quantity);
