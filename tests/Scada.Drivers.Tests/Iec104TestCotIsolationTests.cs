using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104TestCotIsolationTests
{
    [Fact]
    public void CommandTransaction_TestActivationConfirmationDoesNotAdvanceOperationalCommand()
    {
        var transaction = Iec104CommandTransaction.Single(
            commonAddress: 1,
            informationObjectAddress: 77,
            value: true,
            Iec104CommandMode.DirectOperate);
        var request = transaction.CreateInitialRequest();
        var response = Iec104AsduEnvelope.Create(
            request.Header with
            {
                CauseOfTransmission = new Iec104CauseOfTransmission(
                    Iec104CommandTransaction.ActivationConfirmationCause,
                    isTest: true)
            },
            request.Payload.Span);

        var observed = transaction.ObserveResponse(response);

        Assert.False(observed);
        Assert.Equal(Iec104CommandState.AwaitingExecutionConfirmation, transaction.State);
    }

    [Fact]
    public void GeneralInterrogation_TestActivationConfirmationDoesNotAdvanceOperationalGi()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(commonAddress: 1);
        var request = transaction.CreateActivation();
        var response = Iec104AsduEnvelope.Create(
            request.Header with
            {
                CauseOfTransmission = new Iec104CauseOfTransmission(
                    Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
                    isTest: true)
            },
            request.Payload.Span);

        var observed = transaction.ObserveControlResponse(response);

        Assert.False(observed);
        Assert.Equal(Iec104GeneralInterrogationState.AwaitingActivationConfirmation, transaction.State);
    }

    [Fact]
    public async Task SessionRunner_DoesNotPublishTestMarkedProcessTelemetry()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(ioa: 10, value: true, isTest: true),
            CreateSinglePoint(ioa: 11, value: false, isTest: false),
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

        var point = Assert.Single(observed);
        Assert.Equal(11, point.InformationObjectAddress.Value);
        Assert.Equal(false, point.Value);
        Assert.Equal(Iec104GeneralInterrogationState.Completed, runner.GeneralInterrogationStates[1]);
    }

    [Fact]
    public async Task ObservationCollector_DoesNotCreateCandidateFromTestMarkedTelemetry()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(ioa: 20, value: true, isTest: true),
            CreateSinglePoint(ioa: 21, value: true, isTest: false),
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
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(21, candidate.InformationObjectAddress);
        Assert.Equal(1, candidate.ObservationCount);
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

    private static Iec104AsduEnvelope CreateSinglePoint(int ioa, bool value, bool isTest)
    {
        var payload = new byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload.AsSpan(0, 3));
        payload[3] = value ? (byte)1 : (byte)0;
        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3, isTest: isTest),
                CommonAddress: 1),
            payload);
    }

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

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

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
