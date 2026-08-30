using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ReceiveQueueBoundsTests
{
    [Fact]
    public async Task Adapter_ReceiveQueueOverflowFaultsSessionInsteadOfDroppingProcessData()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = RunBurstServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromSeconds(10),
                T2 = TimeSpan.FromSeconds(5),
                T3 = TimeSpan.FromSeconds(20),
                K = 2048,
                W = 2048
            };

            await adapter.ConnectAsync("127.0.0.1", port, options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);

            await WaitUntilAsync(
                () => adapter.GetTransportDiagnostics().SessionFailures >= 1,
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.ProtocolErrors);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Equal(1025, diagnostics.IFramesReceived);
            Assert.Equal(1025, diagnostics.AsdusReceived);
            Assert.Equal(1025, diagnostics.PendingReceiveAcknowledgementCount);
            Assert.Contains("receive queue exceeded 1024 ASDUs", diagnostics.LastFailure ?? string.Empty);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RunBurstServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();

        var start = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, start.Format);
        Assert.Equal(Iec104UFunction.StartDataTransferActivation, start.UFunction);
        await WriteApduAsync(
            stream,
            Iec104ApciFrame.U(Iec104UFunction.StartDataTransferConfirmation),
            cancellationToken);

        var asduBytes = Iec104AsduCodec.Serialize(CreateSinglePoint());
        try
        {
            for (ushort sequence = 0; sequence < 1025; sequence++)
            {
                await WriteApduAsync(
                    stream,
                    Iec104ApciFrame.I(sequence, 0, asduBytes),
                    cancellationToken);
            }
        }
        catch (IOException)
        {
            // The client is expected to close once its bounded queue refuses the 1025th ASDU.
        }
        catch (SocketException)
        {
            // TCP reset is equally acceptable once the bounded queue fails the session.
        }

        await WaitForPeerCloseAsync(stream, cancellationToken);
    }

    private static Iec104AsduEnvelope CreateSinglePoint()
    {
        Span<byte> payload = stackalloc byte[4];
        new Iec104InformationObjectAddress(77).WriteTo(payload[..3]);
        payload[3] = 0x01;
        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3),
                CommonAddress: 1),
            payload);
    }

    private static async Task<Iec104ApciFrame> ReadApduAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[2];
        await ReadExactlyAsync(stream, prefix, cancellationToken);
        var frame = new byte[2 + prefix[1]];
        prefix.CopyTo(frame, 0);
        await ReadExactlyAsync(stream, frame.AsMemory(2), cancellationToken);
        return Iec104ApciCodec.Parse(frame);
    }

    private static async Task WriteApduAsync(
        NetworkStream stream,
        Iec104ApciFrame frame,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Iec104ApciCodec.Serialize(frame), cancellationToken);
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("IEC-104 queue-bound test server observed an unexpected close.");
            offset += read;
        }
    }

    private static async Task WaitForPeerCloseAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        try
        {
            while (await stream.ReadAsync(buffer, cancellationToken) != 0)
            {
            }
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }
}
