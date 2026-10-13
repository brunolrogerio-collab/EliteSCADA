using System.Net.Sockets;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;

namespace Scada.Drivers.Tests;

public sealed class MatterWifiRpcServerTests
{
    [Fact]
    public async Task Authenticated_loopback_bridge_can_read_and_reports_observations()
    {
        await using var rpc = new MatterWifiRpcServer();
        rpc.Start();
        using var client = await ConnectAsync(rpc);
        await rpc.WaitForConnectionAsync();
        var observationReady = new TaskCompletionSource<MatterWifiObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.ObservationReceived += observation => observationReady.TrySetResult(observation);
        await SendAsync(client.Stream, JsonSerializer.Serialize(new
        {
            type = "observation", tagId = client.TagId,
            value = true, observedAt = DateTimeOffset.UtcNow
        }));
        var observation = await observationReady.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(client.TagId, observation.TagId);
        Assert.Equal(JsonValueKind.True, observation.Value.ValueKind);

        var point = new MatterWifiPoint(
            TagDefinition.Create("Relay", "Home.Relay", TagDataType.Boolean, "matter.home", readOnly: false),
            "123", "1/6/0", Writable: true);
        var read = rpc.ReadAsync(point).AsTask();
        using var request = JsonDocument.Parse(await ReadAsync(client.Stream) ?? throw new IOException("RPC server closed before read request."));
        Assert.Equal("read", request.RootElement.GetProperty("op").GetString());
        Assert.Equal("123", request.RootElement.GetProperty("nodeId").GetString());
        Assert.Equal("1/6/0", request.RootElement.GetProperty("address").GetString());
        var id = request.RootElement.GetProperty("id").GetString();
        await SendAsync(client.Stream, JsonSerializer.Serialize(new { id, ok = true, value = true }));
        Assert.Equal(JsonValueKind.True, (await read).ValueKind);
    }

    [Fact]
    public async Task Int64_writes_use_exact_decimal_string_and_uncertain_reply_is_not_replayed()
    {
        await using var rpc = new MatterWifiRpcServer();
        rpc.Start();
        using var client = await ConnectAsync(rpc);
        await rpc.WaitForConnectionAsync();
        var tag = TagDefinition.Create("Counter", "Home.Counter", TagDataType.Int64, "matter.home", readOnly: false);
        var point = new MatterWifiPoint(tag, "18446744073709551615", "1/6/1", Writable: true);
        var write = rpc.WriteAsync(point, 9_007_199_254_740_991L).AsTask();
        using var request = JsonDocument.Parse(await ReadAsync(client.Stream) ?? throw new IOException("RPC server closed before write request."));
        Assert.Equal("write", request.RootElement.GetProperty("op").GetString());
        Assert.Equal(JsonValueKind.String, request.RootElement.GetProperty("value").ValueKind);
        Assert.Equal("9007199254740991", request.RootElement.GetProperty("value").GetString());
        var id = request.RootElement.GetProperty("id").GetString();
        await SendAsync(client.Stream, JsonSerializer.Serialize(new { id, ok = false, uncertain = true, errorCode = "MATTER_WRITE_UNCONFIRMED" }));

        var exception = await Assert.ThrowsAsync<MatterWifiWriteUnconfirmedException>(() => write);
        Assert.Equal(tag.Id, exception.TagId);
        Assert.Equal("MATTER_WRITE_UNCONFIRMED", exception.ErrorCode);
    }

    [Fact]
    public async Task Invalid_token_is_rejected_before_authenticated_readiness()
    {
        await using var rpc = new MatterWifiRpcServer();
        rpc.Start();
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", rpc.Port);
        await SendAsync(client.GetStream(), JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = new string('0', 64) }));
        Assert.Null(await ReadAsync(client.GetStream()));
        await Task.Delay(50);
        Assert.False(rpc.IsConnected);
    }

    private static async Task<TestClient> ConnectAsync(MatterWifiRpcServer rpc)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", rpc.Port);
        await SendAsync(client.GetStream(), JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = rpc.Token }));
        using var welcome = JsonDocument.Parse(await ReadAsync(client.GetStream()) ?? throw new IOException("RPC server closed before welcome."));
        Assert.Equal("welcome", welcome.RootElement.GetProperty("type").GetString());
        return new TestClient(client, client.GetStream(), Guid.NewGuid());
    }

    private static async Task SendAsync(NetworkStream stream, string json)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static async Task<string?> ReadAsync(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var single = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(single);
            if (read == 0) return bytes.Count == 0 ? null : throw new EndOfStreamException();
            if (single[0] == (byte)'\n') return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
            bytes.Add(single[0]);
            if (bytes.Count > 65_536) throw new InvalidDataException("Test frame exceeds limit.");
        }
    }

    private sealed record TestClient(TcpClient Client, NetworkStream Stream, Guid TagId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
