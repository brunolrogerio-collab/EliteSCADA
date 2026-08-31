using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class Iec104ClientSessionRunnerTests
{
    [Fact]
    public async Task RunAsync_ConnectsStartsGiPublishesObservedPointAndStops()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(commonAddress: 1, ioa: 77, value: true),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        };
        var adapter = new FakeAdapter(incoming);
        var runner = new Iec104ClientSessionRunner(
            adapter,
            host: "127.0.0.1",
            port: 2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });
        var observed = new List<Iec104DecodedPoint>();

        await runner.RunAsync((point, _) =>
        {
            observed.Add(point);
            return ValueTask.CompletedTask;
        });

        Assert.True(adapter.ConnectCalled);
        Assert.True(adapter.StartCalled);
        Assert.True(adapter.StopCalled);
        Assert.True(adapter.DisconnectCalled);
        Assert.False(adapter.IsConnected);
        Assert.Equal(Iec104SessionState.Stopped, runner.State);
        Assert.Equal(Iec104GeneralInterrogationState.Completed, runner.GeneralInterrogationStates[1]);
        Assert.Single(adapter.Sent);

        var gi = adapter.Sent[0];
        Assert.Equal(Iec104TypeId.CIcNa1, gi.Header.TypeId);
        Assert.Equal((ushort)1, gi.Header.CommonAddress);

        var point = Assert.Single(observed);
        Assert.Equal((ushort)1, point.CommonAddress);
        Assert.Equal(77, point.InformationObjectAddress.Value);
        Assert.True(Assert.IsType<bool>(point.Value));
    }

    [Fact]
    public async Task RunAsync_MultipleCommonAddresses_SendsOneGiPerAddress()
    {
        var adapter = new FakeAdapter(Array.Empty<Iec104AsduEnvelope>());
        var runner = new Iec104ClientSessionRunner(
            adapter,
            "localhost",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 9, 3, 9 });

        await runner.RunAsync(static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(2, adapter.Sent.Count);
        Assert.Equal(new ushort[] { 3, 9 }, adapter.Sent.Select(static item => item.Header.CommonAddress).ToArray());
    }

    [Fact]
    public async Task RunAsync_AdapterFailure_ReturnsRunnerToStoppedAndPreservesFailure()
    {
        var adapter = new FakeAdapter(Array.Empty<Iec104AsduEnvelope>())
        {
            ConnectFailure = new IOException("synthetic connect failure")
        };
        var runner = new Iec104ClientSessionRunner(
            adapter,
            "localhost",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            runner.RunAsync(static (_, _) => ValueTask.CompletedTask));

        Assert.Equal("synthetic connect failure", exception.Message);
        Assert.Equal(Iec104SessionState.Stopped, runner.State);
    }

    private static Iec104AsduEnvelope CreateSinglePoint(ushort commonAddress, int ioa, bool value)
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MSpNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            commonAddress);
        var payload = new byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload.AsSpan(0, 3));
        payload[3] = value ? (byte)1 : (byte)0;
        return Iec104AsduEnvelope.Create(header, payload);
    }

    private static Iec104AsduEnvelope CreateGiResponse(byte cause)
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.CIcNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(cause),
            CommonAddress: 1);
        return Iec104AsduEnvelope.Create(
            header,
            new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi });
    }

    private sealed class FakeAdapter : IIec104ClientAdapter
    {
        private readonly IReadOnlyList<Iec104AsduEnvelope> _incoming;

        public FakeAdapter(IReadOnlyList<Iec104AsduEnvelope> incoming)
        {
            _incoming = incoming;
        }

        public bool IsConnected { get; private set; }
        public bool ConnectCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool DisconnectCalled { get; private set; }
        public Exception? ConnectFailure { get; init; }
        public List<Iec104AsduEnvelope> Sent { get; } = new();

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            if (ConnectFailure is not null)
                return Task.FromException(ConnectFailure);
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default)
        {
            Sent.Add(asdu);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var asdu in _incoming)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return asdu;
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCalled = true;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
