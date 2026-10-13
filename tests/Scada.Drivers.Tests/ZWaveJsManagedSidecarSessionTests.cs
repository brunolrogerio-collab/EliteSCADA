using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;

namespace Scada.Drivers.Tests;

public sealed class ZWaveJsManagedSidecarSessionTests
{
    [Fact]
    public async Task RpcServer_RequiresLoopbackTokenAndCorrelatesOnlyProcessValueOperations()
    {
        await using var server = new ZWaveJsRpcServer();
        server.Start();
        Assert.True(server.Port is > 0 and <= 65535);
        Assert.Equal(64, server.Token.Length);

        using (var rejected = new TcpClient())
        {
            await rejected.ConnectAsync(IPAddress.Loopback, server.Port);
            await using var rejectedWriter = new StreamWriter(rejected.GetStream(), leaveOpen: true) { AutoFlush = true };
            using var rejectedReader = new StreamReader(rejected.GetStream(), leaveOpen: true);
            await rejectedWriter.WriteLineAsync("{\"type\":\"hello\",\"protocolVersion\":1,\"token\":\"wrong\"}");
            var rejectedLine = await rejectedReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Null(rejectedLine);
        }

        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port);
        await using var writer = new StreamWriter(peer.GetStream(), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(peer.GetStream(), leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = server.Token }));
        using (var welcome = JsonDocument.Parse(await ReadRequiredLineAsync(reader)))
        {
            Assert.Equal("welcome", welcome.RootElement.GetProperty("type").GetString());
            Assert.Equal(1, welcome.RootElement.GetProperty("protocolVersion").GetInt32());
        }

        var point = CreatePoint();
        var readTask = server.ReadAsync(point).AsTask();
        using (var request = JsonDocument.Parse(await ReadRequiredLineAsync(reader)))
        {
            var root = request.RootElement;
            Assert.Equal("read", root.GetProperty("op").GetString());
            Assert.Equal(point.Tag.Id, root.GetProperty("tagId").GetGuid());
            Assert.Equal(point.PortableAddress, root.GetProperty("address").GetString());
            Assert.True(root.GetProperty("writable").GetBoolean());
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = root.GetProperty("id").GetString(), ok = true, value = 17 }));
        }
        Assert.Equal(17, (await readTask).GetInt32());

        var writeTask = server.WriteAsync(point, 21).AsTask();
        using (var request = JsonDocument.Parse(await ReadRequiredLineAsync(reader)))
        {
            var root = request.RootElement;
            Assert.Equal("write", root.GetProperty("op").GetString());
            Assert.Equal(21, root.GetProperty("value").GetInt32());
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = root.GetProperty("id").GetString(), ok = true, value = 21 }));
        }
        Assert.Equal(21, (await writeTask).GetInt32());

        var observed = new TaskCompletionSource<ZWaveJsObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ObservationReceived += value => observed.TrySetResult(value);
        var now = DateTimeOffset.UtcNow;
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { type = "observation", tagId = point.Tag.Id, value = true, observedAt = now }));
        var observation = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(point.Tag.Id, observation.TagId);
        Assert.True(observation.Value.GetBoolean());
        Assert.InRange((observation.ObservedAt - now).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RpcServer_DisconnectFailsPendingOperationWithoutReplayingIt()
    {
        await using var server = new ZWaveJsRpcServer();
        server.Start();
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port);
        await using var writer = new StreamWriter(peer.GetStream(), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(peer.GetStream(), leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = server.Token }));
        _ = await ReadRequiredLineAsync(reader);

        var pending = server.ReadAsync(CreatePoint()).AsTask();
        _ = await ReadRequiredLineAsync(reader);
        peer.Dispose();
        await Assert.ThrowsAsync<IOException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task RpcServer_WriteDisconnectIsOutcomeUnknownAndNeverReplaysTheWrite()
    {
        await using var server = new ZWaveJsRpcServer();
        server.Start();
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port);
        await using var writer = new StreamWriter(peer.GetStream(), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(peer.GetStream(), leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = server.Token }));
        _ = await ReadRequiredLineAsync(reader);

        var pending = server.WriteAsync(CreatePoint(), true).AsTask();
        using var request = JsonDocument.Parse(await ReadRequiredLineAsync(reader));
        Assert.Equal("write", request.RootElement.GetProperty("op").GetString());
        peer.Dispose();

        var error = await Assert.ThrowsAsync<ZWaveJsWriteUnconfirmedException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal("RPC_OUTCOME_UNKNOWN", error.ErrorCode);
    }

    private static ZWaveJsPoint CreatePoint()
    {
        var tag = TagDefinition.Create("Switch", "Home.Switch", TagDataType.Boolean, "zwave", readOnly: false);
        return new ZWaveJsPoint(tag, "node/12/endpoint/0/cc/37/property/string/currentValue/key/none/-", 12, 0, 37, "currentValue", null, true);
    }

    private static async Task<string> ReadRequiredLineAsync(StreamReader reader) =>
        await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)) ?? throw new EndOfStreamException("Expected a Z-Wave RPC frame.");
}
