using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoTimeoutTests
{
    [Fact]
    public async Task DetailedRead_RequestTimeoutMarksPointAndDisconnectsSession()
    {
        await using var peer = new HangingReadPeer();
        var options = Options(peer.Port, requestTimeout: TimeSpan.FromMilliseconds(100));
        var point = Point();
        await using var transport = new S7IsoTransport(options);

        var result = await transport.ReadDetailedAsync(new[] { point });

        Assert.Empty(result.Items);
        Assert.Empty(result.ConfigurationFailures);
        Assert.True(result.CommunicationFailures.ContainsKey(point));
        var diagnostics = transport.GetDiagnostics();
        Assert.False(diagnostics.Connected);
        Assert.Equal(S7IsoFailureKind.Timeout, diagnostics.LastFailureKind);
        Assert.Equal(1L, diagnostics.TimeoutCount);
        Assert.Equal(1L, diagnostics.RequestAttempts);
        Assert.Equal(1L, diagnostics.ConnectionCount);
        Assert.Equal(1L, diagnostics.DisconnectionCount);
    }

    [Fact]
    public async Task DetailedRead_CallerCancellationDoesNotMasqueradeAsTimeoutOrProtocolFault()
    {
        await using var peer = new HangingReadPeer();
        var options = Options(peer.Port, requestTimeout: TimeSpan.FromSeconds(5));
        var point = Point();
        await using var transport = new S7IsoTransport(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transport.ReadDetailedAsync(new[] { point }, cancellation.Token));

        var diagnostics = transport.GetDiagnostics();
        Assert.False(diagnostics.Connected);
        Assert.Null(diagnostics.LastFailureKind);
        Assert.Equal(0L, diagnostics.TimeoutCount);
        Assert.Equal(1L, diagnostics.RequestAttempts);
        Assert.Equal(1L, diagnostics.ConnectionCount);
        Assert.Equal(1L, diagnostics.DisconnectionCount);
    }

    private static S7IsoConnectionOptions Options(int port, TimeSpan requestTimeout) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        connectTimeout: TimeSpan.FromMilliseconds(500),
        requestTimeout: requestTimeout,
        reconnectDelay: TimeSpan.Zero);

    private static S7IsoPoint Point() => new(
        S7IsoTransportTests.Tag(TagDataType.Int16),
        S7IsoArea.Merker,
        0,
        S7IsoValueType.Int16);

    private sealed class HangingReadPeer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;

        public HangingReadPeer()
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

                _ = await ReadPacketAsync(stream, cancellationToken);
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

        private static byte[] SetupResponse(ushort reference)
        {
            var parameter = new byte[]
            {
                0xF0, 0x00,
                0x00, 0x01,
                0x00, 0x01,
                0x01, 0xE0
            };
            var packet = new byte[19 + parameter.Length];
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
            parameter.CopyTo(packet, 19);
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
