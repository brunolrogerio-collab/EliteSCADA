using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;
using Scada.Drivers.ESPHome.Protocol.Generated;

namespace Scada.Drivers.Tests;

public sealed class EspHomeNativeFakePeerTests
{
    [Fact]
    public async Task PlaintextPeer_EnumeratesSubscribesAndCarriesAuthoritativeSwitchState()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();

            var hello = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(EspHomeNativeMessageId.HelloRequest, hello.Type);
            var helloRequest = HelloRequest.Parser.ParseFrom(hello.Payload);
            Assert.Equal(1u, helloRequest.ApiVersionMajor);
            await WriteFrameAsync(stream, EspHomeNativeMessageId.HelloResponse, new HelloResponse
            {
                ApiVersionMajor = 1,
                ApiVersionMinor = 15,
                ServerInfo = "ESPHome 2026.9",
                Name = "fixture"
            }, timeout.Token);

            Assert.Equal(EspHomeNativeMessageId.DeviceInfoRequest, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, EspHomeNativeMessageId.DeviceInfoResponse, new DeviceInfoResponse
            {
                Name = "fixture",
                FriendlyName = "Fixture",
                MacAddress = "AA:BB:CC:DD:EE:FF",
                EsphomeVersion = "2026.9.0",
                Model = "ESP32",
                Manufacturer = "Espressif"
            }, timeout.Token);

            Assert.Equal(EspHomeNativeMessageId.DeviceCapabilitiesRequest, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(
                stream,
                EspHomeNativeMessageId.DeviceCapabilitiesResponse,
                new DeviceCapabilitiesResponse(),
                timeout.Token);

            Assert.Equal(EspHomeEntityMessageId.ListEntitiesRequest, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, EspHomeEntityMessageId.SwitchInfo, new ListEntitiesSwitchResponse
            {
                ObjectId = "relay",
                Key = 0x01020304,
                Name = "Relay",
                EntityCategory = 0
            }, timeout.Token);
            await WriteFrameAsync(stream, EspHomeEntityMessageId.SensorInfo, new ListEntitiesSensorResponse
            {
                ObjectId = "temperature",
                Key = 0x11121314,
                Name = "Temperature",
                EntityCategory = 0,
                DeviceClass = "temperature",
                UnitOfMeasurement = "°C"
            }, timeout.Token);
            await WriteFrameAsync(stream, EspHomeEntityMessageId.ListEntitiesDone, new ListEntitiesDoneResponse(), timeout.Token);

            Assert.Equal(EspHomeEntityMessageId.SubscribeStates, (await ReadFrameAsync(stream, timeout.Token)).Type);
            await WriteFrameAsync(stream, EspHomeEntityMessageId.SwitchState, new SwitchStateResponse
            {
                Key = 0x01020304,
                State = false
            }, timeout.Token);

            var commandFrame = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(EspHomeEntityMessageId.SwitchCommand, commandFrame.Type);
            var command = SwitchCommandRequest.Parser.ParseFrom(commandFrame.Payload);
            Assert.Equal(0x01020304u, command.Key);
            Assert.True(command.State);

            await WriteFrameAsync(stream, EspHomeEntityMessageId.SwitchState, new SwitchStateResponse
            {
                Key = 0x01020304,
                State = true
            }, timeout.Token);
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
        Assert.Equal("AA:BB:CC:DD:EE:FF", inventory.Device.StableDeviceIdentity);
        Assert.Equal(2, inventory.Entities.Count);
        Assert.Contains(inventory.Entities, x => x.Kind == EspHomeEntityKind.Switch && x.Key == 0x01020304);
        Assert.Contains(inventory.Entities, x => x.Kind == EspHomeEntityKind.Sensor && x.DeviceClass == "temperature");

        await client.SubscribeStatesAsync(timeout.Token);
        var initial = await client.ReceiveStateAsync(timeout.Token);
        Assert.Equal(new EspHomeEntityAddress(EspHomeEntityKind.Switch, 0, 0x01020304, "state"), initial.Address);
        Assert.False(Assert.IsType<bool>(initial.Value));

        await client.SendCommandAsync(
            new EspHomeCommand(initial.Address, EspHomeWriteKind.SwitchState, true),
            timeout.Token);
        var updated = await client.ReceiveStateAsync(timeout.Token);
        Assert.True(Assert.IsType<bool>(updated.Value));

        await serverTask;
    }

    private readonly record struct PeerFrame(ushort Type, byte[] Payload);

    private static async Task<PeerFrame> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var marker = new byte[1];
        await ReadExactlyAsync(stream, marker, cancellationToken);
        Assert.Equal((byte)0, marker[0]);
        var length = await ReadVarUInt32Async(stream, cancellationToken);
        var type = await ReadVarUInt32Async(stream, cancellationToken);
        Assert.True(type <= ushort.MaxValue);
        var payload = new byte[checked((int)length)];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return new PeerFrame((ushort)type, payload);
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        ushort type,
        IMessage message,
        CancellationToken cancellationToken)
    {
        var payload = message.ToByteArray();
        using var ms = new MemoryStream();
        ms.WriteByte(0);
        WriteVarUInt32(ms, checked((uint)payload.Length));
        WriteVarUInt32(ms, type);
        ms.Write(payload);
        await stream.WriteAsync(ms.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
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
            var one = new byte[1];
            await ReadExactlyAsync(stream, one, cancellationToken);
            var current = one[0];
            value |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) return value;
        }
        throw new InvalidDataException("Peer varint malformed.");
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> memory, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < memory.Length)
        {
            var read = await stream.ReadAsync(memory[offset..], cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
