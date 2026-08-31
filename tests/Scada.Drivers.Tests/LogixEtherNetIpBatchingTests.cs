using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixEtherNetIpBatchingTests
{
    [Fact]
    public async Task ReadMany_UsesSingleMultipleServicePacketForTwoReads()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();

            var register = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(LogixCipCodec.RegisterSessionCommand, register.Command);
            Assert.Equal(4, register.Payload.Length);
            await WriteFrameAsync(
                stream,
                LogixCipCodec.RegisterSessionCommand,
                0x12345678,
                register.SenderContext,
                [0x01, 0x00, 0x00, 0x00],
                timeout.Token);

            var sendRrData = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(LogixCipCodec.SendRrDataCommand, sendRrData.Command);
            Assert.Equal(0x12345678u, sendRrData.SessionHandle);
            var cipRequest = LogixCipCodec.ExtractCipFromSendRrData(sendRrData.Payload);
            Assert.Equal(LogixCipCodec.MultipleServicePacketService, cipRequest[0]);

            byte[] replyData =
            [
                0x02, 0x00,
                0x06, 0x00,
                0x10, 0x00,
                0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x2A, 0x00, 0x00, 0x00,
                0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0xDC, 0x01, 0x00, 0x00
            ];
            var cipReply = new byte[4 + replyData.Length];
            cipReply[0] = 0x8A;
            replyData.CopyTo(cipReply, 4);
            var cpfReply = LogixCipCodec.BuildSendRrDataPayload(cipReply);
            await WriteFrameAsync(
                stream,
                LogixCipCodec.SendRrDataCommand,
                0x12345678,
                sendRrData.SenderContext,
                cpfReply,
                timeout.Token);
        }, timeout.Token);

        await using var client = new LogixEtherNetIpClient();
        await client.ConnectAsync(new AllenBradleyLogixOptions(
            "127.0.0.1",
            Port: endpoint.Port,
            RequestTimeout: TimeSpan.FromSeconds(2),
            MaxBatchSize: 16), timeout.Token);

        var first = new LogixSymbolReference(LogixTagScope.Controller, "parts", LogixNativeType.Dint);
        var second = new LogixSymbolReference(LogixTagScope.Controller, "ControlWord", LogixNativeType.Dint);
        var results = await client.ReadManyAsync([first, second], timeout.Token);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Succeeded);
        Assert.True(results[1].Succeeded);
        Assert.Equal(42, Assert.IsType<int>(results[0].NativeValue));
        Assert.Equal(476, Assert.IsType<int>(results[1].NativeValue));
        Assert.Equal(2, client.GetDiagnostics().RequestAttempts);
        await server;
    }

    private static async Task<Frame> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[24];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var command = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0, 2));
        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2, 2));
        var session = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        var context = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(12, 8));
        var payload = new byte[payloadLength];
        if (payload.Length > 0) await stream.ReadExactlyAsync(payload, cancellationToken);
        return new Frame(command, session, context, payload);
    }

    private static async Task WriteFrameAsync(
        NetworkStream stream,
        ushort command,
        uint sessionHandle,
        ulong senderContext,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0, 2), command);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2, 2), checked((ushort)payload.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), sessionHandle);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), senderContext);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), 0);
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed record Frame(ushort Command, uint SessionHandle, ulong SenderContext, byte[] Payload);
}
