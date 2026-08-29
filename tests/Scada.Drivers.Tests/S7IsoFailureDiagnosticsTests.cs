using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoFailureDiagnosticsTests
{
    [Fact]
    public async Task CotpReject_IsClassifiedAsIsoConnectionRejected()
    {
        await using var peer = new OneShotPeer(async (stream, cancellationToken) =>
        {
            _ = await ReadPacketAsync(stream, cancellationToken);
            await WritePacketAsync(stream, new byte[]
            {
                0x03, 0x00, 0x00, 0x0B,
                0x06, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00
            }, cancellationToken);
        });
        await using var transport = new S7IsoTransport(Options(peer.Port));

        await Assert.ThrowsAsync<S7IsoProtocolException>(() => transport.ConnectAsync());

        Assert.Equal(S7IsoFailureKind.IsoConnectionRejected, transport.GetDiagnostics().LastFailureKind);
        Assert.False(transport.GetDiagnostics().Connected);
    }

    [Fact]
    public async Task SetupCommunicationError_IsClassifiedAsS7SessionRejected()
    {
        await using var peer = new OneShotPeer(async (stream, cancellationToken) =>
        {
            _ = await ReadPacketAsync(stream, cancellationToken);
            await WritePacketAsync(stream, ConnectionConfirm(), cancellationToken);
            var setup = await ReadPacketAsync(stream, cancellationToken);
            var reference = BinaryPrimitives.ReadUInt16BigEndian(setup.AsSpan(11, 2));
            await WritePacketAsync(stream, SetupError(reference), cancellationToken);
        });
        await using var transport = new S7IsoTransport(Options(peer.Port));

        await Assert.ThrowsAsync<S7IsoProtocolException>(() => transport.ConnectAsync());

        Assert.Equal(S7IsoFailureKind.S7SessionRejected, transport.GetDiagnostics().LastFailureKind);
        Assert.False(transport.GetDiagnostics().Connected);
    }

    [Fact]
    public async Task DroppedPollingSession_ExposesTransportUnavailableInCommonDiagnostics()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x12, 0x34 });
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var options = new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            port: server.Port,
            reconnectDelay: TimeSpan.FromMilliseconds(300));
        await using var driver = new S7IsoDriver(
            "s7-failure-diagnostics",
            "S7 Failure Diagnostics",
            options,
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(50));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        server.DropActiveConnection();
        await WaitUntilAsync(
            () =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Reconnecting &&
                       diagnostics.ProtocolDetails is not null &&
                       diagnostics.ProtocolDetails.TryGetValue("lastFailureKind", out var kind) &&
                       kind == nameof(S7IsoFailureKind.TransportUnavailable);
            },
            TimeSpan.FromSeconds(2));

        var current = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x1234, Assert.IsType<short>(current.Value));
        Assert.Equal(TagQuality.BadCommunication, current.Quality);
        Assert.Equal(
            nameof(S7IsoFailureKind.TransportUnavailable),
            driver.GetCommunicationDiagnostics().ProtocolDetails!["lastFailureKind"]);
    }

    private static S7IsoConnectionOptions Options(int port) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        reconnectDelay: TimeSpan.Zero);

    private static byte[] ConnectionConfirm() => new byte[]
    {
        0x03, 0x00, 0x00, 0x0B,
        0x06, 0xD0, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    private static byte[] SetupError(ushort reference)
    {
        var packet = new byte[19];
        packet[0] = 0x03;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[4] = 0x02;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        packet[7] = 0x32;
        packet[8] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(11, 2), reference);
        packet[17] = 0x05;
        packet[18] = 0x01;
        return packet;
    }

    private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var packet = new byte[length];
        header.CopyTo(packet, 0);
        if (length > 4) await ReadExactAsync(stream, packet.AsMemory(4), cancellationToken);
        return packet;
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static async Task WritePacketAsync(NetworkStream stream, byte[] packet, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(packet, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.True(condition(), $"Condition was not met within {timeout}.");
    }

    private sealed class OneShotPeer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;

        public OneShotPeer(Func<NetworkStream, CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _task = RunAsync(handler, _cts.Token);
        }

        public int Port { get; }

        private async Task RunAsync(Func<NetworkStream, CancellationToken, Task> handler, CancellationToken cancellationToken)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await handler(client.GetStream(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try { await _task; }
            catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}