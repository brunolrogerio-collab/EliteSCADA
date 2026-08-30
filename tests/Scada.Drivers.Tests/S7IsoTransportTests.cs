using System.Buffers.Binary;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoTransportTests
{
    [Fact]
    public async Task ConnectReadWrite_NegotiatesPduAndUsesTypedDbAddress()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.DataBlock, 1, 0, new byte[] { 0x12, 0x34 });
        var options = Options(server.Port);
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true);
        await using var transport = new S7IsoTransport(options);

        await transport.ConnectAsync();
        var read = Assert.Single(await transport.ReadAsync(new[] { point }));

        Assert.True(read.Succeeded);
        Assert.Equal((short)0x1234, Assert.IsType<short>(S7IsoValueCodec.Decode(point, read.Data!)));
        Assert.Equal((ushort)480, transport.GetDiagnostics().NegotiatedPduSize);

        await transport.WriteAsync(point, S7IsoValueCodec.Encode(point, (short)0x4567));

        Assert.Equal(new byte[] { 0x45, 0x67 }, server.GetBytes(S7IsoArea.DataBlock, 1, 0, 2));
        var diagnostics = transport.GetDiagnostics();
        Assert.True(diagnostics.Connected);
        Assert.Equal(2L, diagnostics.RequestAttempts);
        Assert.Equal(1L, diagnostics.ConnectionCount);
        Assert.Equal(1, diagnostics.LastReadBatchCount);
        Assert.Equal(1, diagnostics.LastReadPointCount);
    }

    [Fact]
    public async Task ReadAcrossInputOutputMerkerAndDb_PreservesAreaIdentityAndPadding()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.Input, 0, 0, new byte[] { 0x11 });
        server.SetBytes(S7IsoArea.Output, 0, 1, new byte[] { 0x22 });
        server.SetBytes(S7IsoArea.Merker, 0, 2, new byte[] { 0x33 });
        server.SetBytes(S7IsoArea.DataBlock, 5, 3, new byte[] { 0x44 });

        var points = new[]
        {
            new S7IsoPoint(Tag(TagDataType.Int16), S7IsoArea.Input, 0, S7IsoValueType.Byte),
            new S7IsoPoint(Tag(TagDataType.Int16), S7IsoArea.Output, 1, S7IsoValueType.Byte),
            new S7IsoPoint(Tag(TagDataType.Int16), S7IsoArea.Merker, 2, S7IsoValueType.Byte),
            new S7IsoPoint(Tag(TagDataType.Int16), S7IsoArea.DataBlock, 3, S7IsoValueType.Byte, DbNumber: 5)
        };
        await using var transport = new S7IsoTransport(Options(server.Port));

        var results = await transport.ReadAsync(points);

        Assert.Equal(4, results.Count);
        Assert.Equal((short)0x11, Assert.IsType<short>(S7IsoValueCodec.Decode(points[0], results[0].Data!)));
        Assert.Equal((short)0x22, Assert.IsType<short>(S7IsoValueCodec.Decode(points[1], results[1].Data!)));
        Assert.Equal((short)0x33, Assert.IsType<short>(S7IsoValueCodec.Decode(points[2], results[2].Data!)));
        Assert.Equal((short)0x44, Assert.IsType<short>(S7IsoValueCodec.Decode(points[3], results[3].Data!)));
        Assert.Equal(1L, transport.GetDiagnostics().RequestAttempts);
    }

    [Fact]
    public async Task WriteOutputAndMerker_UpdatesOnlyRequestedProcessAreas()
    {
        await using var server = new TestS7IsoServer();
        var output = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Output,
            4,
            S7IsoValueType.Byte,
            Writable: true);
        var merker = new S7IsoPoint(
            Tag(TagDataType.Boolean),
            S7IsoArea.Merker,
            6,
            S7IsoValueType.Boolean,
            BitOffset: 3,
            Writable: true);
        await using var transport = new S7IsoTransport(Options(server.Port));

        await transport.WriteAsync(output, S7IsoValueCodec.Encode(output, (short)0x5A));
        await transport.WriteAsync(merker, S7IsoValueCodec.Encode(merker, true));

        Assert.Equal(new byte[] { 0x5A }, server.GetBytes(S7IsoArea.Output, 0, 4, 1));
        Assert.Equal(new byte[] { 0x08 }, server.GetBytes(S7IsoArea.Merker, 0, 6, 1));
        Assert.Equal(2L, transport.GetDiagnostics().RequestAttempts);
    }

    [Fact]
    public async Task DroppedSession_FailsOnceThenReconnectsAndRenegotiates()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.Merker, 0, 20, new byte[] { 0x12, 0x34 });
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            20,
            S7IsoValueType.Int16);
        await using var transport = new S7IsoTransport(Options(server.Port));

        var initial = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.Equal((short)0x1234, Assert.IsType<short>(S7IsoValueCodec.Decode(point, initial.Data!)));

        server.DropActiveConnection();
        var failure = await Record.ExceptionAsync(async () =>
        {
            await transport.ReadAsync(new[] { point });
        });
        Assert.NotNull(failure);
        Assert.True(
            failure is IOException or SocketException or ObjectDisposedException,
            $"Unexpected dropped-session exception: {failure.GetType().FullName}: {failure.Message}");

        server.SetBytes(S7IsoArea.Merker, 0, 20, new byte[] { 0x45, 0x67 });
        var recovered = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.Equal((short)0x4567, Assert.IsType<short>(S7IsoValueCodec.Decode(point, recovered.Data!)));

        var diagnostics = transport.GetDiagnostics();
        Assert.True(diagnostics.Connected);
        Assert.Equal((ushort)480, diagnostics.NegotiatedPduSize);
        Assert.Equal(2L, diagnostics.ConnectionCount);
        Assert.Equal(1L, diagnostics.ReconnectCount);
        Assert.True(diagnostics.DisconnectionCount >= 1);
        Assert.True(diagnostics.RequestAttempts >= 3);
    }

    [Fact]
    public async Task DetailedRead_LaterBatchLossReturnsCompletedItemsAndRemainingCommunicationFailures()
    {
        await using var server = new TestS7IsoServer(240)
        {
            DropBeforeDataRequestNumber = 2
        };
        var points = Enumerable.Range(0, 30)
            .Select(index =>
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(bytes, index + 100);
                server.SetBytes(S7IsoArea.Merker, 0, index * 4, bytes);
                return new S7IsoPoint(
                    Tag(TagDataType.Int32),
                    S7IsoArea.Merker,
                    index * 4,
                    S7IsoValueType.Int32);
            })
            .ToArray();
        await using var transport = new S7IsoTransport(Options(server.Port));

        var result = await transport.ReadDetailedAsync(points);

        Assert.Equal(19, result.Items.Count);
        Assert.Empty(result.ConfigurationFailures);
        Assert.Equal(11, result.CommunicationFailures.Count);
        for (var index = 0; index < result.Items.Count; index++)
        {
            Assert.Same(points[index], result.Items[index].Point);
            Assert.Equal(index + 100, Assert.IsType<int>(S7IsoValueCodec.Decode(points[index], result.Items[index].Data!)));
        }
        Assert.All(points.Skip(19), point => Assert.Contains(point, result.CommunicationFailures.Keys));
        var diagnostics = transport.GetDiagnostics();
        Assert.False(diagnostics.Connected);
        Assert.Equal(S7IsoFailureKind.TransportUnavailable, diagnostics.LastFailureKind);
        Assert.Equal(2L, diagnostics.RequestAttempts);
        Assert.Equal(1L, diagnostics.DisconnectionCount);
        Assert.Equal(2, diagnostics.LastReadBatchCount);
        Assert.Equal(30, diagnostics.LastReadPointCount);
    }

    [Fact]
    public async Task OversizedPoint_IsRejectedLocallyAgainstNegotiatedPdu()
    {
        await using var server = new TestS7IsoServer(240);
        var point = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.String,
            DbNumber: 1,
            Writable: true,
            StringLength: 254);
        await using var transport = new S7IsoTransport(Options(server.Port));

        await transport.ConnectAsync();

        await Assert.ThrowsAsync<S7IsoConfigurationException>(async () =>
        {
            await transport.ReadAsync(new[] { point });
        });
        await Assert.ThrowsAsync<S7IsoConfigurationException>(async () =>
        {
            await transport.WriteAsync(point, S7IsoValueCodec.Encode(point, "TEST"));
        });

        var diagnostics = transport.GetDiagnostics();
        Assert.Equal((ushort)240, diagnostics.NegotiatedPduSize);
        Assert.Equal(0L, diagnostics.RequestAttempts);
        Assert.True(diagnostics.Connected);
    }

    internal static S7IsoConnectionOptions Options(int port) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        reconnectDelay: TimeSpan.Zero,
        writeEnabled: true);

    internal static TagDefinition Tag(TagDataType type, bool readOnly = false) => new(
        Guid.NewGuid(),
        "T",
        $"PLC.{Guid.NewGuid():N}",
        type,
        "s7",
        null,
        null,
        readOnly);
}
