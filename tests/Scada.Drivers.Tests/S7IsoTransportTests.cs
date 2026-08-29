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
    }

    internal static S7IsoConnectionOptions Options(int port) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        reconnectDelay: TimeSpan.Zero);

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
