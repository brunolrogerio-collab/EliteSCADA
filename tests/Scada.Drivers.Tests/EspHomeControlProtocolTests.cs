using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

public sealed class EspHomeControlProtocolTests
{
    [Fact]
    public async Task RawPeer_PingIsAcknowledged_AndDisconnectIsAcknowledgedThenSurfaced()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            Assert.Equal((ushort)1, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 2, Proto(
                VarintField(1, 1),
                VarintField(2, 15),
                StringField(3, "ESPHome 2026.9"),
                StringField(4, "control-fixture")), timeout.Token);

            Assert.Equal((ushort)9, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 10, Proto(
                StringField(2, "control-fixture"),
                StringField(3, "AA:BB:CC:DD:EE:FF"),
                StringField(4, "2026.9.0"),
                StringField(6, "ESP32"),
                StringField(12, "Espressif"),
                StringField(13, "Control Fixture")), timeout.Token);

            Assert.Equal((ushort)11, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 17, Proto(
                StringField(1, "relay"),
                Fixed32Field(2, 0x01020304),
                StringField(3, "Relay")), timeout.Token);
            await WriteFrameAsync(stream, 19, [], timeout.Token);

            Assert.Equal((ushort)20, (await ReadFrameAsync(stream, timeout.Token)).Type);

            await WriteFrameAsync(stream, 7, [], timeout.Token);
            var pingResponse = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)8, pingResponse.Type);
            Assert.Empty(pingResponse.Payload);

            await WriteFrameAsync(stream, 26, Fixed32Field(1, 0x01020304), timeout.Token);

            await WriteFrameAsync(stream, 5, VarintField(1, 1), timeout.Token);
            var disconnectResponse = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)6, disconnectResponse.Type);
            Assert.Empty(disconnectResponse.Payload);
        }, timeout.Token);

        await using var client = new EspHomeNativeClient(
            new EspHomeConnectionSettings(
                "127.0.0.1",
                endpoint.Port,
                EspHomeNativeEncryptionMode.Plaintext,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1)),
            _ => ValueTask.FromResult<EspHomeResolvedNoiseKey?>(null));

        _ = await client.ConnectAsync(timeout.Token);
        await client.SubscribeStatesAsync(timeout.Token);

        var state = await client.ReceiveStateAsync(timeout.Token);
        Assert.Equal(new EspHomeEntityAddress(EspHomeEntityKind.Switch, 0, 0x01020304, "state"), state.Address);
        Assert.False(Assert.IsType<bool>(state.Value));

        var disconnect = await Assert.ThrowsAsync<EspHomeRemoteDisconnectException>(
            async () => await client.ReceiveStateAsync(timeout.Token));
        Assert.Equal(1, disconnect.Reason);

        await peer;
    }

    private readonly record struct RawFrame(ushort Type, byte[] Payload);

    private static async Task<RawFrame> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var marker = await ReadByteAsync(stream, cancellationToken);
        Assert.Equal((byte)0, marker);
        var length = await ReadVarUInt32Async(stream, cancellationToken);
        var type = await ReadVarUInt32Async(stream, cancellationToken);
        var payload = new byte[checked((int)length)];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return new RawFrame(checked((ushort)type), payload);
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        ushort type,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        using var frame = new MemoryStream();
        frame.WriteByte(0);
        WriteVarUInt32(frame, checked((uint)payload.Length));
        WriteVarUInt32(frame, type);
        frame.Write(payload);
        await stream.WriteAsync(frame.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static byte[] Proto(params byte[][] fields)
    {
        using var result = new MemoryStream();
        foreach (var field in fields) result.Write(field);
        return result.ToArray();
    }

    private static byte[] VarintField(int number, uint value)
    {
        using var field = new MemoryStream();
        WriteVarUInt32(field, checked((uint)(number << 3)));
        WriteVarUInt32(field, value);
        return field.ToArray();
    }

    private static byte[] Fixed32Field(int number, uint value)
    {
        using var field = new MemoryStream();
        WriteVarUInt32(field, checked((uint)((number << 3) | 5)));
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        field.Write(bytes);
        return field.ToArray();
    }

    private static byte[] StringField(int number, string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value);
        using var field = new MemoryStream();
        WriteVarUInt32(field, checked((uint)((number << 3) | 2)));
        WriteVarUInt32(field, checked((uint)utf8.Length));
        field.Write(utf8);
        return field.ToArray();
    }

    private static void WriteVarUInt32(Stream stream, uint value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        stream.WriteByte((byte)value);
    }

    private static async Task<uint> ReadVarUInt32Async(Stream stream, CancellationToken cancellationToken)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var current = await ReadByteAsync(stream, cancellationToken);
            value |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) return value;
        }
        throw new InvalidDataException("Raw peer varint is malformed.");
    }

    private static async Task<byte> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var one = new byte[1];
        await ReadExactlyAsync(stream, one, cancellationToken);
        return one[0];
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
