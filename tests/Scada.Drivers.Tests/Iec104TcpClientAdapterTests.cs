using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104TcpClientAdapterTests
{
    [Fact]
    public async Task Adapter_ExchangesStartDataInformationAcknowledgementAndStopFrames()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = RunBasicExchangeServerAsync(listener, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromSeconds(2),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(5),
                K = 2,
                W = 1
            };

            await adapter.ConnectAsync("127.0.0.1", port, options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);
            Assert.True(adapter.IsConnected);

            var interrogation = new Iec104GeneralInterrogationTransaction(1);
            await adapter.SendAsync(interrogation.CreateActivation(), timeout.Token);

            await using var reader = adapter.ReadAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
            Assert.True(await reader.MoveNextAsync());
            var received = reader.Current;
            Assert.Equal(Iec104TypeId.MSpNa1, received.Header.TypeId);
            Assert.Equal((ushort)1, received.Header.CommonAddress);

            var decoded = Iec104InformationObjectDecoder.Decode(received, TimeZoneInfo.Utc);
            var point = Assert.Single(decoded);
            Assert.Equal(77, point.InformationObjectAddress.Value);
            Assert.Equal(true, point.Value);

            await adapter.StopDataTransferAsync(timeout.Token);
            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
            Assert.False(adapter.IsConnected);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Adapter_RepliesToRemoteTestFrameActivation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testConfirmed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = RunTestFrameServerAsync(listener, testConfirmed, timeout.Token);
            await using var adapter = new Iec104TcpClientAdapter();
            var options = new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(2),
                T1 = TimeSpan.FromSeconds(2),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(5),
                K = 12,
                W = 8
            };

            await adapter.ConnectAsync("127.0.0.1", port, options, timeout.Token);
            await adapter.StartDataTransferAsync(timeout.Token);
            await testConfirmed.Task.WaitAsync(timeout.Token);
            await adapter.StopDataTransferAsync(timeout.Token);
            await adapter.DisconnectAsync(timeout.Token);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RunBasicExchangeServerAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();

        var start = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, start.Format);
        Assert.Equal(Iec104UFunction.StartDataTransferActivation, start.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StartDataTransferConfirmation), cancellationToken);

        var request = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.I, request.Format);
        Assert.Equal((ushort)0, request.SendSequence);
        Assert.Equal((ushort)0, request.ReceiveSequence);
        var requestAsdu = Iec104AsduCodec.Parse(request.Asdu.Span);
        Assert.Equal(Iec104TypeId.CIcNa1, requestAsdu.Header.TypeId);
        Assert.Equal((ushort)1, requestAsdu.Header.CommonAddress);

        await WriteApduAsync(stream, Iec104ApciFrame.S(1), cancellationToken);

        var observedAsdu = CreateSinglePoint(commonAddress: 1, ioa: 77, value: true);
        var observedBytes = Iec104AsduCodec.Serialize(observedAsdu);
        await WriteApduAsync(stream, Iec104ApciFrame.I(0, 1, observedBytes), cancellationToken);

        var supervisory = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.S, supervisory.Format);
        Assert.Equal((ushort)1, supervisory.ReceiveSequence);

        var stop = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, stop.Format);
        Assert.Equal(Iec104UFunction.StopDataTransferActivation, stop.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StopDataTransferConfirmation), cancellationToken);
    }

    private static async Task RunTestFrameServerAsync(
        TcpListener listener,
        TaskCompletionSource<bool> testConfirmed,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();

        var start = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104UFunction.StartDataTransferActivation, start.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StartDataTransferConfirmation), cancellationToken);

        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.TestFrameActivation), cancellationToken);
        var confirmation = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104ApciFrameFormat.U, confirmation.Format);
        Assert.Equal(Iec104UFunction.TestFrameConfirmation, confirmation.UFunction);
        testConfirmed.TrySetResult(true);

        var stop = await ReadApduAsync(stream, cancellationToken);
        Assert.Equal(Iec104UFunction.StopDataTransferActivation, stop.UFunction);
        await WriteApduAsync(stream, Iec104ApciFrame.U(Iec104UFunction.StopDataTransferConfirmation), cancellationToken);
    }

    private static Iec104AsduEnvelope CreateSinglePoint(ushort commonAddress, int ioa, bool value)
    {
        Span<byte> payload = stackalloc byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload[..3]);
        payload[3] = value ? (byte)0x01 : (byte)0x00;

        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3),
                commonAddress),
            payload);
    }

    private static async Task<Iec104ApciFrame> ReadApduAsync(NetworkStream stream, CancellationToken cancellationToken)
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
                throw new EndOfStreamException("Loopback IEC-104 server received an unexpected TCP close.");
            offset += read;
        }
    }
}
