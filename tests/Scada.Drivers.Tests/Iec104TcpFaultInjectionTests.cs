using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104TcpFaultInjectionTests
{
    [Fact]
    public async Task Adapter_InvalidPeerAcknowledgementFaultsSessionAsProtocolError()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var serverTask = RunInvalidAcknowledgementServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), StableOptions(), timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);
            await adapter.SendAsync(new Iec104GeneralInterrogationTransaction(1).CreateActivation(), timeout.Token);

            await WaitUntilAsync(
                () => adapter.GetTransportDiagnostics().ProtocolErrors >= 1,
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.ProtocolErrors);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Equal(1, diagnostics.UnacknowledgedSendCount);
            Assert.Contains("acknowledgement", diagnostics.LastFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var serverTask = RunOutOfOrderInformationServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), StableOptions(), timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);

            await WaitUntilAsync(
                () => adapter.GetTransportDiagnostics().ProtocolErrors >= 1,
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.ProtocolErrors);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Equal(0, diagnostics.AsdusReceived);
            Assert.Contains("sequence", diagnostics.LastFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_PartialApduEofFaultsSessionWithoutMisclassifyingProtocolError()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var serverTask = RunPartialApduServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), StableOptions(), timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);

            await WaitUntilAsync(
                () => adapter.GetTransportDiagnostics().SessionFailures >= 1,
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Equal(0, diagnostics.ProtocolErrors);
            Assert.Contains("peer closed", diagnostics.LastFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_T1ExpiresWhenPeerNeverAcknowledgesOutstandingIFrame()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromMilliseconds(350),
                T2 = TimeSpan.FromMilliseconds(100),
                T3 = TimeSpan.FromSeconds(5),
                K = 2,
                W = 2
            };
            var serverTask = RunNoAcknowledgementServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);
            await adapter.SendAsync(new Iec104GeneralInterrogationTransaction(1).CreateActivation(), timeout.Token);

            // T1Timeouts is incremented immediately before the supervisor throws. Wait for the
            // complete failed-session transition that the assertions below actually verify.
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = adapter.GetTransportDiagnostics();
                    return snapshot.T1Timeouts >= 1 && snapshot.SessionFailures >= 1 && !snapshot.IsConnected;
                },
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.T1Timeouts);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Equal(0, diagnostics.ProtocolErrors);
            Assert.Equal(1, diagnostics.UnacknowledgedSendCount);
            Assert.Contains("T1", diagnostics.LastFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acknowledgementObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromSeconds(2),
                T2 = TimeSpan.FromMilliseconds(150),
                T3 = TimeSpan.FromSeconds(5),
                K = 2,
                W = 2
            };
            var serverTask = RunDelayedAcknowledgementServerAsync(listener, acknowledgementObserved, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);

            await acknowledgementObserved.Task.WaitAsync(timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.True(diagnostics.IsConnected);
            Assert.Equal(1, diagnostics.T2Expirations);
            Assert.Equal(0, diagnostics.SessionFailures);
            Assert.Equal(0, diagnostics.PendingReceiveAcknowledgementCount);
            Assert.True(diagnostics.SFramesSent >= 1);

            await adapter.StopDataTransferAsync(timeout.Token);
            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_T3TestFrameWithoutConfirmationFailsUnderT1()
    {
        var listener = StartListener();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromMilliseconds(250),
                T2 = TimeSpan.FromMilliseconds(100),
                T3 = TimeSpan.FromMilliseconds(150),
                K = 2,
                W = 2
            };
            var serverTask = RunUnconfirmedTestFrameServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            await adapter.ConnectAsync("127.0.0.1", GetPort(listener), options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);

            await WaitUntilAsync(
                () => adapter.GetTransportDiagnostics().SessionFailures >= 1,
                timeout.Token);

            var diagnostics = adapter.GetTransportDiagnostics();
            Assert.False(diagnostics.IsConnected);
            Assert.True(diagnostics.T3Expirations >= 1);
            Assert.True(diagnostics.T1Timeouts >= 1);
            Assert.True(diagnostics.TestFrameActivationsSent >= 1);
            Assert.Equal(0, diagnostics.TestFrameConfirmationsReceived);
            Assert.Equal(1, diagnostics.SessionFailures);
            Assert.Contains("TESTFR con", diagnostics.LastFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static TcpListener StartListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int GetPort(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static Iec104SessionOptions StableOptions() => new()
    {
        T0 = TimeSpan.FromSeconds(2),
        T1 = TimeSpan.FromSeconds(2),
        T2 = TimeSpan.FromSeconds(1),
        T3 = TimeSpan.FromSeconds(5),
        K = 2,
        W = 2
    };

    private static async Task RunInvalidAcknowledgementServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        var request = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.I, request.Format);
        Assert.Equal((ushort)0, request.SendSequence);
        await WriteApduAsync(stream, Iec104ApciFrame.S(2), cancellationToken);
        await WaitForPeerCloseAsync(stream, cancellationToken);
    }

    private static async Task RunOutOfOrderInformationServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        var asdu = Iec104AsduCodec.Serialize(CreateSinglePoint(ioa: 77));
        await WriteApduAsync(stream, Iec104ApciFrame.I(1, 0, asdu), cancellationToken);
        await WaitForPeerCloseAsync(stream, cancellationToken);
    }

    private static async Task RunPartialApduServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        await stream.WriteAsync(new byte[] { 0x68, 0x0E, 0x00, 0x00 }, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        client.Client.Shutdown(SocketShutdown.Send);
    }

    private static async Task RunNoAcknowledgementServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        var request = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.I, request.Format);
        await WaitForPeerCloseAsync(stream, cancellationToken);
    }

    private static async Task RunDelayedAcknowledgementServerAsync(
        TcpListener listener,
        TaskCompletionSource<bool> acknowledgementObserved,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        var asdu = Iec104AsduCodec.Serialize(CreateSinglePoint(ioa: 88));
        await WriteApduAsync(stream, Iec104ApciFrame.I(0, 0, asdu), cancellationToken);

        var acknowledgement = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.S, acknowledgement.Format);
        Assert.Equal((ushort)1, acknowledgement.ReceiveSequence);
        acknowledgementObserved.TrySetResult(true);

        var stop = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104UFunction.StopDataTransferActivation, stop.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StopDataTransferConfirmation), cancellationToken);
    }

    private static async Task RunUnconfirmedTestFrameServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();
        await CompleteStartHandshakeAsync(stream, cancellationToken);

        var test = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, test.Format);
        Assert.Equal(Iec104UFunction.TestFrameActivation, test.UFunction);
        await WaitForPeerCloseAsync(stream, cancellationToken);
    }

    private static async Task CompleteStartHandshakeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var start = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, start.Format);
        Assert.Equal(Iec104UFunction.StartDataTransferActivation, start.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StartDataTransferConfirmation), cancellationToken);
    }

    private static Iec104AsduEnvelope CreateSinglePoint(int ioa)
    {
        Span<byte> payload = stackalloc byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload[..3]);
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
        await stream.FlushAsync(cancellationToken);
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
                throw new EndOfStreamException("Fault-injection server observed an unexpected TCP close.");
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
            // TCP reset is also an acceptable failed-session close.
        }
        catch (SocketException)
        {
            // TCP reset is also an acceptable failed-session close.
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }
}