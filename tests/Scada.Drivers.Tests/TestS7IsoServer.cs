using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

internal sealed class TestS7IsoServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _memoryGate = new();
    private readonly Dictionary<(byte Area, ushort Db), byte[]> _memory = new();
    private readonly Task _loop;

    public TestS7IsoServer()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _loop = RunAsync(_cts.Token);
    }

    public int Port { get; }

    public void SetBytes(S7IsoArea area, ushort dbNumber, int byteOffset, ReadOnlySpan<byte> data)
    {
        lock (_memoryGate)
        {
            var memory = GetMemory((byte)area, dbNumber);
            data.CopyTo(memory.AsSpan(byteOffset));
        }
    }

    public byte[] GetBytes(S7IsoArea area, ushort dbNumber, int byteOffset, int length)
    {
        lock (_memoryGate)
        {
            return GetMemory((byte)area, dbNumber).AsSpan(byteOffset, length).ToArray();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        if (await ReadPacketAsync(stream, cancellationToken) is null) return;
        await WritePacketAsync(stream, ConnectionConfirm(), cancellationToken);

        var setup = await ReadPacketAsync(stream, cancellationToken);
        if (setup is null) return;
        var setupReference = BinaryPrimitives.ReadUInt16BigEndian(setup.AsSpan(11, 2));
        await WritePacketAsync(
            stream,
            AckData(
                setupReference,
                new byte[] { 0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0xE0 },
                Array.Empty<byte>()),
            cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await ReadPacketAsync(stream, cancellationToken);
            if (request is null) return;
            if (request.Length < 19) return;

            var reference = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(11, 2));
            switch (request[17])
            {
                case 0x04:
                    await WritePacketAsync(stream, HandleRead(reference, request), cancellationToken);
                    break;
                case 0x05:
                    await WritePacketAsync(stream, HandleWrite(reference, request), cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }

    private byte[] HandleRead(ushort reference, byte[] request)
    {
        var count = request[18];
        var data = new List<byte>();
        for (var item = 0; item < count; item++)
        {
            var specOffset = 19 + item * 12;
            var transport = request[specOffset + 3];
            var elementCount = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(specOffset + 4, 2));
            var dbNumber = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(specOffset + 6, 2));
            var area = request[specOffset + 8];
            var addressBits = Read24(request.AsSpan(specOffset + 9, 3));
            var payload = ReadPayload(area, dbNumber, addressBits, transport, elementCount);

            data.Add(0xFF);
            data.Add(transport == 0x01 ? (byte)0x03 : (byte)0x04);
            var encodedLength = transport == 0x01 ? 1 : checked(payload.Length * 8);
            data.Add((byte)(encodedLength >> 8));
            data.Add((byte)encodedLength);
            data.AddRange(payload);
            if (item < count - 1 && (payload.Length & 1) != 0) data.Add(0x00);
        }

        return AckData(reference, new byte[] { 0x04, count }, data.ToArray());
    }

    private byte[] HandleWrite(ushort reference, byte[] request)
    {
        var specOffset = 19;
        var transport = request[specOffset + 3];
        var dbNumber = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(specOffset + 6, 2));
        var area = request[specOffset + 8];
        var addressBits = Read24(request.AsSpan(specOffset + 9, 3));
        var parameterLength = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(13, 2));
        var dataOffset = 17 + parameterLength;
        var encodedLength = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(dataOffset + 2, 2));
        var payloadLength = request[dataOffset + 1] == 0x03 ? 1 : (encodedLength + 7) / 8;
        var payload = request.AsSpan(dataOffset + 4, payloadLength);

        WritePayload(area, dbNumber, addressBits, transport, payload);
        return AckData(reference, new byte[] { 0x05, 0x01 }, new byte[] { 0xFF });
    }

    private byte[] ReadPayload(byte area, ushort dbNumber, int addressBits, byte transport, ushort elementCount)
    {
        lock (_memoryGate)
        {
            var memory = GetMemory(area, dbNumber);
            var byteOffset = addressBits / 8;
            if (transport == 0x01)
            {
                var bit = addressBits % 8;
                return new[] { (byte)(((memory[byteOffset] >> bit) & 1) != 0 ? 1 : 0) };
            }

            var byteCount = transport switch
            {
                0x02 => elementCount,
                0x04 or 0x05 => checked(elementCount * 2),
                0x06 or 0x07 or 0x08 => checked(elementCount * 4),
                _ => throw new InvalidOperationException($"Unsupported test S7 transport 0x{transport:X2}.")
            };
            return memory.AsSpan(byteOffset, byteCount).ToArray();
        }
    }

    private void WritePayload(byte area, ushort dbNumber, int addressBits, byte transport, ReadOnlySpan<byte> payload)
    {
        lock (_memoryGate)
        {
            var memory = GetMemory(area, dbNumber);
            var byteOffset = addressBits / 8;
            if (transport == 0x01)
            {
                var bit = addressBits % 8;
                if (payload[0] == 0) memory[byteOffset] &= (byte)~(1 << bit);
                else memory[byteOffset] |= (byte)(1 << bit);
                return;
            }
            payload.CopyTo(memory.AsSpan(byteOffset));
        }
    }

    private byte[] GetMemory(byte area, ushort dbNumber)
    {
        var key = (area, dbNumber);
        if (!_memory.TryGetValue(key, out var memory))
        {
            memory = new byte[65536];
            _memory[key] = memory;
        }
        return memory;
    }

    private static byte[] ConnectionConfirm() => new byte[]
    {
        0x03, 0x00, 0x00, 0x0B,
        0x06, 0xD0, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    private static byte[] AckData(ushort reference, byte[] parameter, byte[] data)
    {
        var packet = new byte[19 + parameter.Length + data.Length];
        packet[0] = 0x03;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[4] = 0x02;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        packet[7] = 0x32;
        packet[8] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(11, 2), reference);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(13, 2), checked((ushort)parameter.Length));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(15, 2), checked((ushort)data.Length));
        parameter.CopyTo(packet, 19);
        data.CopyTo(packet, 19 + parameter.Length);
        return packet;
    }

    private static int Read24(ReadOnlySpan<byte> value) => (value[0] << 16) | (value[1] << 8) | value[2];

    private static async Task<byte[]?> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        if (!await ReadExactOrEofAsync(stream, header, cancellationToken)) return null;
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        if (length < 4) throw new InvalidDataException("Invalid test TPKT length.");
        var packet = new byte[length];
        header.CopyTo(packet, 0);
        if (length > 4 && !await ReadExactOrEofAsync(stream, packet.AsMemory(4, length - 4), cancellationToken))
            throw new EndOfStreamException();
        return packet;
    }

    private static async Task<bool> ReadExactOrEofAsync(NetworkStream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    private static async Task WritePacketAsync(NetworkStream stream, byte[] packet, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(packet, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        try { await _loop; }
        catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
