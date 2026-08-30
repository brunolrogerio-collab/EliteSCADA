using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixProtocolBoundaryValidationTests
{
    [Fact]
    public async Task ReadMany_WrongCipReplyServiceFailsClosedAndDropsSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();
            await CompleteRegistrationAsync(stream, timeout.Token);

            var request = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(LogixCipCodec.SendRrDataCommand, request.Command);
            Assert.Equal(SessionHandle, request.SessionHandle);

            // Reply is structurally successful but claims Write Tag response (0xCD)
            // for a Read Tag request (0x4C -> expected 0xCC).
            var wrongServiceReply = new byte[] { 0xCD, 0x00, 0x00, 0x00 };
            var cpfReply = LogixCipCodec.BuildSendRrDataPayload(wrongServiceReply);
            await WriteFrameAsync(
                stream,
                LogixCipCodec.SendRrDataCommand,
                SessionHandle,
                request.SenderContext,
                cpfReply,
                timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port);
        await client.ConnectAsync(CreateOptions(endpoint.Port), timeout.Token);

        var reference = new LogixSymbolReference(LogixTagScope.Controller, "parts", LogixNativeType.Dint);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await client.ReadManyAsync([reference], timeout.Token));

        Assert.Contains("response service", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsConnected);
        var diagnostics = client.GetDiagnostics();
        Assert.Equal(1, diagnostics.FailedRequests);
        Assert.Equal(1, diagnostics.Disconnections);
        await server;
    }

    [Fact]
    public async Task ReadMany_MismatchedEncapsulationSessionFailsClosedAndDropsSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();
            await CompleteRegistrationAsync(stream, timeout.Token);

            var request = await ReadFrameAsync(stream, timeout.Token);
            Assert.Equal(LogixCipCodec.SendRrDataCommand, request.Command);
            Assert.Equal(SessionHandle, request.SessionHandle);

            var readReply = new byte[]
            {
                0xCC, 0x00, 0x00, 0x00,
                0xC4, 0x00,
                0x2A, 0x00, 0x00, 0x00
            };
            var cpfReply = LogixCipCodec.BuildSendRrDataPayload(readReply);
            await WriteFrameAsync(
                stream,
                LogixCipCodec.SendRrDataCommand,
                0x87654321,
                request.SenderContext,
                cpfReply,
                timeout.Token);
        }, timeout.Token);

        await using var client = CreateClient(endpoint.Port);
        await client.ConnectAsync(CreateOptions(endpoint.Port), timeout.Token);

        var reference = new LogixSymbolReference(LogixTagScope.Controller, "parts", LogixNativeType.Dint);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await client.ReadManyAsync([reference], timeout.Token));

        Assert.Contains("active session", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsConnected);
        var diagnostics = client.GetDiagnostics();
        Assert.Equal(1, diagnostics.FailedRequests);
        Assert.Equal(1, diagnostics.Disconnections);
        await server;
    }

    private const uint SessionHandle = 0x12345678;

    private static LogixEtherNetIpClient CreateClient(int port) => new();

    private static AllenBradleyLogixOptions CreateOptions(int port) =>
        new(
            "127.0.0.1",
            Port: port,
            RequestTimeout: TimeSpan.FromSeconds(2),
            MaxBatchSize: 16);

    private static async Task CompleteRegistrationAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var register = await ReadFrameAsync(stream, cancellationToken);
        Assert.Equal(LogixCipCodec.RegisterSessionCommand, register.Command);
        Assert.Equal(0u, register.SessionHandle);
        await WriteFrameAsync(
            stream,
            LogixCipCodec.RegisterSessionCommand,
            SessionHandle,
            register.SenderContext,
            [0x01, 0x00, 0x00, 0x00],
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
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), senderContext);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), 0);
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed record Frame(ushort Command, uint SessionHandle, ulong SenderContext, byte[] Payload);
}
