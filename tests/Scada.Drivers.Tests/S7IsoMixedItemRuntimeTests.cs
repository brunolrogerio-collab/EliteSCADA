using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoMixedItemRuntimeTests
{
    [Fact]
    public async Task OneFailedItem_DoesNotPoisonHealthyItemOrDisconnectSession()
    {
        await using var peer = new MixedReadPeer();
        var badTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var goodTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var badPoint = new S7IsoPoint(badTag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        var goodPoint = new S7IsoPoint(goodTag, S7IsoArea.Merker, 2, S7IsoValueType.Int16);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var options = new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            port: peer.Port,
            reconnectDelay: TimeSpan.Zero);
        await using var driver = new S7IsoDriver(
            "s7-mixed-items",
            "S7 Mixed Items",
            options,
            cache,
            registry,
            new[] { badPoint, goodPoint },
            TimeSpan.FromSeconds(5));

        await driver.StartAsync();
        await WaitUntilAsync(
            () =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.TagQuality.BadConfiguration == 1 && diagnostics.TagQuality.Good == 1;
            },
            TimeSpan.FromSeconds(2));

        var bad = Assert.IsType<TagValue>((await driver.ReadAsync(badTag.Id))!);
        var good = Assert.IsType<TagValue>((await driver.ReadAsync(goodTag.Id))!);
        Assert.Equal(TagQuality.BadConfiguration, bad.Quality);
        Assert.Null(bad.Value);
        Assert.Equal(TagQuality.Good, good.Quality);
        Assert.Equal((short)0x1234, Assert.IsType<short>(good.Value));

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        Assert.Equal(nameof(S7IsoFailureKind.AddressInvalid), diagnostics.ProtocolDetails!["lastFailureKind"]);
        Assert.Equal(1L, diagnostics.Counters.Connections);
        Assert.Equal(0L, diagnostics.Counters.Disconnections);
        Assert.Equal(0L, diagnostics.Counters.Reconnects);
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

    private sealed class MixedReadPeer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;

        public MixedReadPeer()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _task = RunAsync(_cts.Token);
        }

        public int Port { get; }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                _ = await ReadPacketAsync(stream, cancellationToken);
                await WritePacketAsync(stream, ConnectionConfirm(), cancellationToken);

                var setup = await ReadPacketAsync(stream, cancellationToken);
                var setupReference = BinaryPrimitives.ReadUInt16BigEndian(setup.AsSpan(11, 2));
                await WritePacketAsync(stream, SetupResponse(setupReference), cancellationToken);

                var read = await ReadPacketAsync(stream, cancellationToken);
                var readReference = BinaryPrimitives.ReadUInt16BigEndian(read.AsSpan(11, 2));
                await WritePacketAsync(stream, MixedReadResponse(readReference), cancellationToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (EndOfStreamException) { }
            catch (IOException) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try { await _task; }
            catch (OperationCanceledException) { }
            _cts.Dispose();
        }

        private static byte[] ConnectionConfirm() => new byte[]
        {
            0x03, 0x00, 0x00, 0x0B,
            0x06, 0xD0, 0x00, 0x01, 0x00, 0x00, 0x00
        };

        private static byte[] SetupResponse(ushort reference) => AckData(
            reference,
            new byte[]
            {
                0xF0, 0x00,
                0x00, 0x01,
                0x00, 0x01,
                0x01, 0xE0
            },
            Array.Empty<byte>());

        private static byte[] MixedReadResponse(ushort reference) => AckData(
            reference,
            new byte[] { 0x04, 0x02 },
            new byte[]
            {
                0x05, 0x00, 0x00, 0x04,
                0xFF, 0x05, 0x00, 0x10, 0x12, 0x34
            });

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
    }
}
