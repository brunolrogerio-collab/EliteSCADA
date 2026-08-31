using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixRoutedBatchFallbackTests
{
    [Theory]
    [InlineData((byte)0x1A)]
    [InlineData((byte)0x1B)]
    public async Task ReadMany_RoutedBridgePacketTooLargeSplitsMspIntoSingleReads(byte bridgeStatus)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observedEmbeddedServices = new List<byte>();

        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();

            var register = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(LogixCipCodec.RegisterSessionCommand, register.Command);
            await WriteFrameAsync(
                stream,
                LogixCipCodec.RegisterSessionCommand,
                0x12345678,
                register.SenderContext,
                [0x01, 0x00, 0x00, 0x00],
                timeout.Token);

            var batchFrame = await ReadFrameAsync(stream, timeout.Token);
            var batchOuter = LogixCipCodec.ExtractCipFromSendRrData(batchFrame.Payload);
            var batchEmbedded = ExtractEmbeddedRequest(batchOuter);
            observedEmbeddedServices.Add(batchEmbedded[0]);
            Assert.Equal(LogixCipCodec.MultipleServicePacketService, batchEmbedded[0]);
            await ReplyWithRoutedFailureAsync(stream, batchFrame, bridgeStatus, timeout.Token);

            var firstFrame = await ReadFrameAsync(stream, timeout.Token);
            var firstOuter = LogixCipCodec.ExtractCipFromSendRrData(firstFrame.Payload);
            var firstEmbedded = ExtractEmbeddedRequest(firstOuter);
            observedEmbeddedServices.Add(firstEmbedded[0]);
            Assert.Equal(LogixCipCodec.ReadTagService, firstEmbedded[0]);
            await ReplyWithRoutedDintAsync(stream, firstFrame, 42, timeout.Token);

            var secondFrame = await ReadFrameAsync(stream, timeout.Token);
            var secondOuter = LogixCipCodec.ExtractCipFromSendRrData(secondFrame.Payload);
            var secondEmbedded = ExtractEmbeddedRequest(secondOuter);
            observedEmbeddedServices.Add(secondEmbedded[0]);
            Assert.Equal(LogixCipCodec.ReadTagService, secondEmbedded[0]);
            await ReplyWithRoutedDintAsync(stream, secondFrame, 476, timeout.Token);
        }, timeout.Token);

        await using var client = new LogixEtherNetIpClient();
        await client.ConnectAsync(new AllenBradleyLogixOptions(
            "127.0.0.1",
            Port: endpoint.Port,
            Route: [new CipRouteSegment(1, 0)],
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
        Assert.Equal(
            [LogixCipCodec.MultipleServicePacketService, LogixCipCodec.ReadTagService, LogixCipCodec.ReadTagService],
            observedEmbeddedServices);
        await server;
    }

    [Theory]
    [InlineData((byte)0x1A)]
    [InlineData((byte)0x1B)]
    public void MapGeneralStatus_ClassifiesBridgePacketSizeFailuresAsPacketTooLarge(byte status)
    {
        Assert.Equal(LogixProtocolError.PacketTooLarge, LogixCipCodec.MapGeneralStatus(status));
    }

    private static byte[] ExtractEmbeddedRequest(byte[] routedRequest)
    {
        Assert.True(routedRequest.Length >= 10);
        Assert.Equal(LogixCipCodec.UnconnectedSendService, routedRequest[0]);
        var pathBytes = routedRequest[1] * 2;
        var dataOffset = 2 + pathBytes;
        Assert.True(routedRequest.Length >= dataOffset + 4);
        var messageLength = BinaryPrimitives.ReadUInt16LittleEndian(routedRequest.AsSpan(dataOffset + 2, 2));
        var messageOffset = dataOffset + 4;
        Assert.True(routedRequest.Length >= messageOffset + messageLength);
        return routedRequest.AsSpan(messageOffset, messageLength).ToArray();
    }

    private static async Task ReplyWithRoutedFailureAsync(
        NetworkStream stream,
        Frame request,
        byte generalStatus,
        CancellationToken cancellationToken)
    {
        byte[] outerReply = [0xD2, 0x00, generalStatus, 0x00];
        var cpfReply = LogixCipCodec.BuildSendRrDataPayload(outerReply);
        await WriteFrameAsync(
            stream,
            LogixCipCodec.SendRrDataCommand,
            request.SessionHandle,
            request.SenderContext,
            cpfReply,
            cancellationToken);
    }

    private static async Task ReplyWithRoutedDintAsync(
        NetworkStream stream,
        Frame request,
        int value,
        CancellationToken cancellationToken)
    {
        var innerReply = new byte[10];
        innerReply[0] = 0xCC;
        BinaryPrimitives.WriteUInt16LittleEndian(innerReply.AsSpan(4, 2), LogixValueCodec.CipTypeDint);
        BinaryPrimitives.WriteInt32LittleEndian(innerReply.AsSpan(6, 4), value);

        var outerReply = new byte[4 + innerReply.Length];
        outerReply[0] = 0xD2;
        innerReply.CopyTo(outerReply, 4);
        var cpfReply = LogixCipCodec.BuildSendRrDataPayload(outerReply);
        await WriteFrameAsync(
            stream,
            LogixCipCodec.SendRrDataCommand,
            request.SessionHandle,
            request.SenderContext,
            cpfReply,
            cancellationToken);
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
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), senderContext);
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed record Frame(ushort Command, uint SessionHandle, ulong SenderContext, byte[] Payload);
}
