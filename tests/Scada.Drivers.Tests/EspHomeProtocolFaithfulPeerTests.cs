using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

/// <summary>
/// Protocol-faithful peer that deliberately does not use the EliteSCADA protobuf
/// classes or frame codec on the server side. Payload bytes are assembled from
/// the official ESPHome field numbers/wire types, so this catches shared-codec
/// mistakes that a generated-message fake peer could hide.
/// </summary>
public sealed class EspHomeProtocolFaithfulPeerTests
{
    [Fact]
    public async Task RawOfficialWirePeer_EnumeratesStateAndAcceptsSwitchCommand()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            var hello = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)1, hello.Type);
            Assert.True(ContainsSequence(hello.Payload, [0x10, 0x01, 0x18, 0x0F]));
            await WriteFrameAsync(stream, 2, Proto(
                VarintField(1, 1),
                VarintField(2, 15),
                StringField(3, "ESPHome 2026.9"),
                StringField(4, "raw-fixture")), timeout.Token);

            var deviceInfoRequest = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)9, deviceInfoRequest.Type);
            Assert.Empty(deviceInfoRequest.Payload);
            await WriteFrameAsync(stream, 10, Proto(
                StringField(2, "raw-fixture"),
                StringField(3, "AA:BB:CC:DD:EE:FF"),
                StringField(4, "2026.9.0"),
                StringField(6, "ESP32"),
                StringField(12, "Espressif"),
                StringField(13, "Raw Fixture")), timeout.Token);

            var capabilitiesRequest = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)149, capabilitiesRequest.Type);
            Assert.Empty(capabilitiesRequest.Payload);
            await WriteFrameAsync(stream, 150, [], timeout.Token);

            var listRequest = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)11, listRequest.Type);
            Assert.Empty(listRequest.Payload);

            const uint switchKey = 0x01020304;
            await WriteFrameAsync(stream, 17, Proto(
                StringField(1, "relay"),
                Fixed32Field(2, switchKey),
                StringField(3, "Relay")), timeout.Token);
            await WriteFrameAsync(stream, 19, [], timeout.Token);

            var subscribe = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)20, subscribe.Type);
            Assert.Empty(subscribe.Payload);

            // false is protobuf's default, so the authoritative false state needs
            // only the forced fixed32 entity key on the wire.
            await WriteFrameAsync(stream, 26, Proto(
                Fixed32Field(1, switchKey)), timeout.Token);

            var command = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)33, command.Type);
            Assert.Equal(
                new byte[] { 0x0D, 0x04, 0x03, 0x02, 0x01, 0x10, 0x01 },
                command.Payload);

            await WriteFrameAsync(stream, 26, Proto(
                Fixed32Field(1, switchKey),
                VarintField(2, 1)), timeout.Token);
        }, timeout.Token);

        await using var client = new EspHomeNativeClient(
            new EspHomeConnectionSettings(
                "127.0.0.1",
                endpoint.Port,
                EspHomeNativeEncryptionMode.Plaintext,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1)),
            _ => ValueTask.FromResult<EspHomeResolvedNoiseKey?>(null));

        var inventory = await client.ConnectAsync(timeout.Token);
        Assert.Equal(new EspHomeApiVersion(1, 15), inventory.NegotiatedVersion);
        Assert.Equal("AA:BB:CC:DD:EE:FF", inventory.Device.StableDeviceIdentity);
        var entity = Assert.Single(inventory.Entities);
        Assert.Equal(EspHomeEntityKind.Switch, entity.Kind);
        Assert.Equal(0x01020304u, entity.Key);
        Assert.Equal("relay", entity.ObjectId);

        await client.SubscribeStatesAsync(timeout.Token);
        var initial = await client.ReceiveStateAsync(timeout.Token);
        Assert.Equal(new EspHomeEntityAddress(EspHomeEntityKind.Switch, 0, 0x01020304, "state"), initial.Address);
        Assert.False(Assert.IsType<bool>(initial.Value));

        await client.SendCommandAsync(
            new EspHomeCommand(initial.Address, EspHomeWriteKind.SwitchState, true),
            timeout.Token);

        var authoritative = await client.ReceiveStateAsync(timeout.Token);
        Assert.True(Assert.IsType<bool>(authoritative.Value));
        await peer;
    }

    private readonly record struct RawFrame(ushort Type, byte[] Payload);

    private static async Task<RawFrame> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var marker = await ReadByteAsync(stream, cancellationToken);
        Assert.Equal((byte)0, marker);
        var length = await ReadVarUInt32Async(stream, cancellationToken);
        var type = await ReadVarUInt32Async(stream, cancellationToken);
        Assert.True(type <= ushort.MaxValue);
        var payload = new byte[checked((int)length)];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return new RawFrame((ushort)type, payload);
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
        WriteVarUInt32(field, checked((uint)((number << 3) | 0)));
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

    private static bool ContainsSequence(ReadOnlySpan<byte> data, ReadOnlySpan<byte> sequence) =>
        data.IndexOf(sequence) >= 0;

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
            if (shift == 28 && (current & 0xF0) != 0)
                throw new InvalidDataException("Raw peer varint exceeds uint32.");
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
