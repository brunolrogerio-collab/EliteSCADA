using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

public sealed class EspHomeDeviceCapabilitiesTests
{
    [Fact]
    public async Task Api114_DoesNotRequestDeviceCapabilities()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            Assert.Equal((ushort)1, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 2, HelloPayload(14), timeout.Token);

            Assert.Equal((ushort)9, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 10, DeviceInfoPayload(), timeout.Token);

            var next = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)11, next.Type);
            Assert.Empty(next.Payload);
            await WriteFrameAsync(stream, 19, [], timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port, TimeSpan.FromMilliseconds(500));
        var inventory = await client.ConnectAsync(timeout.Token);

        Assert.Equal(new EspHomeApiVersion(1, 14), inventory.NegotiatedVersion);
        Assert.Null(inventory.DeviceCapabilities);
        await peer;
    }

    [Fact]
    public async Task Api115_RequestsAndPreservesDeviceCapabilitiesMetadata()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            Assert.Equal((ushort)1, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 2, HelloPayload(16), timeout.Token);

            Assert.Equal((ushort)9, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 10, DeviceInfoPayload(), timeout.Token);

            var capabilitiesRequest = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal((ushort)149, capabilitiesRequest.Type);
            Assert.Empty(capabilitiesRequest.Payload);

            var capabilities = Proto(
                MessageField(1, Proto(
                    VarintField(1, 5),
                    StringField(2, "AC:BC:32:89:0E:AA"))),
                MessageField(2, Proto(
                    VarintField(1, 3))),
                MessageField(3, Proto(
                    VarintField(1, 9),
                    VarintField(2, 0x12345678))),
                MessageField(4, Proto(
                    StringField(1, "bus-a"),
                    VarintField(2, 2),
                    VarintField(3, 7))),
                // Additive unknown response field must remain tolerated.
                VarintField(31, 77));
            await WriteFrameAsync(stream, 150, capabilities, timeout.Token);

            Assert.Equal((ushort)11, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 19, [], timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port, TimeSpan.FromMilliseconds(500));
        var inventory = await client.ConnectAsync(timeout.Token);

        Assert.Equal(new EspHomeApiVersion(1, 15), inventory.NegotiatedVersion);
        var capabilities = Assert.IsType<EspHomeDeviceCapabilities>(inventory.DeviceCapabilities);
        Assert.True(capabilities.BluetoothProxyPresent);
        Assert.Equal(5u, capabilities.BluetoothProxyFeatureFlags);
        Assert.Equal("AC:BC:32:89:0E:AA", capabilities.BluetoothProxyMacAddress);
        Assert.True(capabilities.VoiceAssistantPresent);
        Assert.Equal(3u, capabilities.VoiceAssistantFeatureFlags);
        Assert.True(capabilities.ZWaveProxyPresent);
        Assert.Equal(9u, capabilities.ZWaveProxyFeatureFlags);
        Assert.Equal(0x12345678u, capabilities.ZWaveHomeId);
        var serial = Assert.Single(capabilities.SerialProxies);
        Assert.Equal("bus-a", serial.Name);
        Assert.Equal(7u, serial.ConfiguredLineStates);
        Assert.False(string.IsNullOrWhiteSpace(serial.PortType));

        var materialization = EspHomeEntityMapper.BuildMaterialization(inventory);
        Assert.Empty(materialization.Tags ?? []);
        Assert.Empty(materialization.Commands ?? []);

        await peer;
    }

    [Fact]
    public async Task Api115_MalformedCapabilitiesResponse_FailsClosed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            Assert.Equal((ushort)1, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 2, HelloPayload(15), timeout.Token);
            Assert.Equal((ushort)9, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 10, DeviceInfoPayload(), timeout.Token);
            Assert.Equal((ushort)149, (await ReadFrameAsync(stream, timeout.Token)).Type);

            // Field 1 declares a 5-byte nested message but supplies one byte.
            await WriteFrameAsync(stream, 150, [0x0A, 0x05, 0x08], timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port, TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAsync<InvalidProtocolBufferException>(
            async () => await client.ConnectAsync(timeout.Token));
        await peer;
    }

    [Fact]
    public async Task Api115_WrongCapabilitiesResponse_IsBoundedByProtocolTimeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();

            Assert.Equal((ushort)1, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 2, HelloPayload(15), timeout.Token);
            Assert.Equal((ushort)9, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, 10, DeviceInfoPayload(), timeout.Token);
            Assert.Equal((ushort)149, (await ReadFrameAsync(stream, timeout.Token)).Type);

            // A valid but wrong message is ignored by the existing expected-message
            // reader, then the bounded request timeout terminates the wait.
            await WriteFrameAsync(stream, 19, [], timeout.Token);
            await Task.Delay(500, timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port, TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.ConnectAsync(timeout.Token));
        await peer;
    }

    private static EspHomeNativeClient CreateClient(int port, TimeSpan requestTimeout) =>
        new(
            new EspHomeConnectionSettings(
                "127.0.0.1",
                port,
                EspHomeNativeEncryptionMode.Plaintext,
                requestTimeout,
                TimeSpan.FromSeconds(1)),
            _ => ValueTask.FromResult<EspHomeResolvedNoiseKey?>(null));

    private static byte[] HelloPayload(uint minor) => Proto(
        VarintField(1, 1),
        VarintField(2, minor),
        StringField(3, "ESPHome capabilities fixture"),
        StringField(4, "caps-fixture"));

    private static byte[] DeviceInfoPayload() => Proto(
        StringField(2, "caps-fixture"),
        StringField(3, "AA:BB:CC:DD:EE:FF"),
        StringField(4, "2026.9.0"),
        StringField(6, "ESP32"),
        StringField(12, "Espressif"),
        StringField(13, "Capabilities Fixture"));

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

    private static byte[] MessageField(int number, byte[] payload)
    {
        using var field = new MemoryStream();
        WriteVarUInt32(field, checked((uint)((number << 3) | 2)));
        WriteVarUInt32(field, checked((uint)payload.Length));
        field.Write(payload);
        return field.ToArray();
    }

    private static byte[] VarintField(int number, uint value)
    {
        using var field = new MemoryStream();
        WriteVarUInt32(field, checked((uint)(number << 3)));
        WriteVarUInt32(field, value);
        return field.ToArray();
    }

    private static byte[] StringField(int number, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return MessageField(number, bytes);
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
