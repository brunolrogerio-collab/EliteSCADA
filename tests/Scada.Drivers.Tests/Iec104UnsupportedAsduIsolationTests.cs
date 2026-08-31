using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104UnsupportedAsduIsolationTests
{
    [Fact]
    public async Task SessionRunner_UnsupportedTypeDoesNotPublishOperationalPoint()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            Iec104AsduEnvelope.Create(
                new Iec104AsduHeader(
                    (Iec104TypeId)127,
                    ObjectCount: 1,
                    IsSequence: false,
                    new Iec104CauseOfTransmission(causeCode: 3),
                    CommonAddress: 1),
                new byte[] { 77, 0, 0, 0xAA }),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        };
        var adapter = new FakeAdapter(incoming);
        var runner = new Iec104ClientSessionRunner(
            adapter,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });
        var observed = new List<Iec104DecodedPoint>();

        await runner.RunAsync((point, _) =>
        {
            observed.Add(point);
            return ValueTask.CompletedTask;
        });

        Assert.Empty(observed);
        Assert.Equal(Iec104GeneralInterrogationState.Completed, runner.GeneralInterrogationStates[1]);
    }

    [Fact]
    public async Task ObservationCollector_UnsupportedTypeDoesNotCreateEngineeringCandidate()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            Iec104AsduEnvelope.Create(
                new Iec104AsduHeader(
                    (Iec104TypeId)127,
                    ObjectCount: 1,
                    IsSequence: false,
                    new Iec104CauseOfTransmission(causeCode: 3),
                    CommonAddress: 1),
                new byte[] { 88, 0, 0, 0x55 }),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        };
        var collector = new Iec104ObservationCollector(
            () => new FakeAdapter(incoming),
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });

        var result = await collector.ObserveAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.AllRequestedGeneralInterrogationsCompleted);
        Assert.Empty(result.Candidates);
    }

    private static Iec104AsduEnvelope CreateGiResponse(byte cause) =>
        Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.CIcNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(cause),
                CommonAddress: 1),
            new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi });

    private sealed class FakeAdapter : IIec104ClientAdapter
    {
        private readonly IReadOnlyList<Iec104AsduEnvelope> _incoming;

        public FakeAdapter(IReadOnlyList<Iec104AsduEnvelope> incoming)
        {
            _incoming = incoming;
        }

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask SendAsync(
            Iec104AsduEnvelope asdu,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

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
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
