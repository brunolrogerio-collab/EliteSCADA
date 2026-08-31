using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixRegisterSessionValidationTests
{
    public static TheoryData<byte[], string> InvalidRegisterSessionPayloads => new()
    {
        { new byte[] { 0x02, 0x00, 0x00, 0x00 }, "protocol version" },
        { new byte[] { 0x01, 0x00, 0x01, 0x00 }, "session options" },
        { new byte[] { 0x01, 0x00, 0x00 }, "exactly 4 bytes" }
    };

    [Theory]
    [MemberData(nameof(InvalidRegisterSessionPayloads))]
    public async Task Connect_InvalidRegisterSessionCommandDataFailsClosed(
        byte[] replyPayload,
        string expectedMessage)
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
            Assert.Equal(0u, register.SessionHandle);
            Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x00 }, register.Payload);

            await WriteFrameAsync(
                stream,
                LogixCipCodec.RegisterSessionCommand,
                SessionHandle,
                register.SenderContext,
                replyPayload,
                timeout.Token);
        }, timeout.Token);

        await using var client = new LogixEtherNetIpClient();
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await client.ConnectAsync(CreateOptions(endpoint.Port), timeout.Token);
        });

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsConnected);
        var diagnostics = client.GetDiagnostics();
        Assert.Equal(1, diagnostics.RequestAttempts);
        Assert.Equal(1, diagnostics.FailedRequests);
        Assert.Equal(0, diagnostics.ConnectionCount);
        Assert.NotNull(diagnostics.LastError);
        Assert.Contains(expectedMessage, diagnostics.LastError, StringComparison.OrdinalIgnoreCase);
        await server;
    }

    [Fact]
    public async Task Connect_NonZeroEncapsulationOptionsFailsClosed()
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

            await WriteFrameAsync(
                stream,
                LogixCipCodec.RegisterSessionCommand,
                SessionHandle,
                register.SenderContext,
                new byte[] { 0x01, 0x00, 0x00, 0x00 },
                timeout.Token,
                headerOptions: 1);
        }, timeout.Token);

        await using var client = new LogixEtherNetIpClient();
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await client.ConnectAsync(CreateOptions(endpoint.Port), timeout.Token);
        });

        Assert.Contains("Options must be zero", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsConnected);
        var diagnostics = client.GetDiagnostics();
        Assert.Equal(1, diagnostics.RequestAttempts);
        Assert.Equal(1, diagnostics.FailedRequests);
        Assert.Equal(0, diagnostics.ConnectionCount);
        Assert.NotNull(diagnostics.LastError);
        Assert.Contains("Options must be zero", diagnostics.LastError, StringComparison.OrdinalIgnoreCase);
        await server;
    }

    private const uint SessionHandle = 0x12345678;

    private static AllenBradleyLogixOptions CreateOptions(int port) =>
        new(
            "127.0.0.1",
            Port: port,
            RequestTimeout: TimeSpan.FromSeconds(2),
            MaxBatchSize: 16);

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
        CancellationToken cancellationToken,
        uint headerOptions = 0)
    {
        var header = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0, 2), command);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2, 2), checked((ushort)payload.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), sessionHandle);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), senderContext);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), headerOptions);
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed record Frame(ushort Command, uint SessionHandle, ulong SenderContext, byte[] Payload);
}
