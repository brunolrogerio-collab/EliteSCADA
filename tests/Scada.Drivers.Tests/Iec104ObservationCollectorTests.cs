using System.Runtime.CompilerServices;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ObservationCollectorTests
{
    [Fact]
    public async Task ObserveAsync_CollectsGiEvidenceAndFlagsTypeConflict()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(1, 100, true, cause: 20),
            CreateDoublePoint(1, 100, Iec104DoublePointState.On, cause: 3),
            CreateSinglePoint(1, 101, false, cause: 3),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        };
        var adapter = new FakeAdapter(incoming);
        var collector = CreateCollector(() => adapter);

        var result = await collector.ObserveAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.IsPartial);
        Assert.True(result.AllRequestedGeneralInterrogationsCompleted);
        Assert.False(result.CandidateLimitReached);
        Assert.Equal(Iec104GeneralInterrogationState.Completed, result.GeneralInterrogationStates[1]);
        Assert.Equal(2, result.Candidates.Count);

        var conflicted = result.Candidates.Single(candidate => candidate.InformationObjectAddress == 100);
        Assert.True(conflicted.HasTypeConflict);
        Assert.Equal(new[] { Iec104TypeId.MSpNa1, Iec104TypeId.MDpNa1 }, conflicted.ObservedTypeIds);
        Assert.Null(conflicted.SuggestedDataType);
        Assert.Equal(2, conflicted.ObservationCount);
        Assert.Equal((byte)3, conflicted.LastCauseOfTransmission);
        Assert.Equal(Iec104DoublePointState.On, conflicted.LastValue);

        var stable = result.Candidates.Single(candidate => candidate.InformationObjectAddress == 101);
        Assert.False(stable.HasTypeConflict);
        Assert.Equal(TagDataType.Boolean, stable.SuggestedDataType);
        Assert.Equal(false, stable.LastValue);
        Assert.Equal(TagQuality.Good, stable.LastQuality);

        Assert.Equal(1, adapter.ConnectCount);
        Assert.Equal(1, adapter.StartCount);
        Assert.Equal(1, adapter.StopCount);
        Assert.Equal(1, adapter.DisconnectCount);
        Assert.Single(adapter.Sent);
        Assert.Equal(Iec104TypeId.CIcNa1, adapter.Sent[0].Header.TypeId);
    }

    [Fact]
    public async Task ObserveAsync_CandidateLimitStopsCollectionAndRemainsPartial()
    {
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(1, 10, true, cause: 20),
            CreateSinglePoint(1, 11, false, cause: 20),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        };
        var adapter = new FakeAdapter(incoming);
        var collector = CreateCollector(() => adapter);

        var result = await collector.ObserveAsync(TimeSpan.FromSeconds(1), maximumCandidates: 1);

        Assert.True(result.IsPartial);
        Assert.True(result.CandidateLimitReached);
        Assert.False(result.AllRequestedGeneralInterrogationsCompleted);
        Assert.Single(result.Candidates);
        Assert.Equal(10, result.Candidates.Single().InformationObjectAddress);
        Assert.Equal(Iec104GeneralInterrogationState.Collecting, result.GeneralInterrogationStates[1]);
        Assert.Equal(1, adapter.StopCount);
        Assert.Equal(1, adapter.DisconnectCount);
    }

    [Fact]
    public async Task ObserveAsync_StreamEndsWithoutActivationTermination_IsNotReportedComplete()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(1, 42, true, cause: 20)
        });
        var collector = CreateCollector(() => adapter);

        var result = await collector.ObserveAsync(TimeSpan.FromSeconds(1));

        Assert.False(result.AllRequestedGeneralInterrogationsCompleted);
        Assert.Equal(Iec104GeneralInterrogationState.Collecting, result.GeneralInterrogationStates[1]);
        Assert.Single(result.Candidates);
    }

    private static Iec104ObservationCollector CreateCollector(Func<IIec104ClientAdapter> factory) =>
        new(
            factory,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });

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

    private static Iec104AsduEnvelope CreateSinglePoint(ushort commonAddress, int ioa, bool value, byte cause)
    {
        var payload = new byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload.AsSpan(0, 3));
        payload[3] = value ? (byte)1 : (byte)0;
        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(cause),
                commonAddress),
            payload);
    }

    private static Iec104AsduEnvelope CreateDoublePoint(
        ushort commonAddress,
        int ioa,
        Iec104DoublePointState value,
        byte cause)
    {
        var payload = new byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload.AsSpan(0, 3));
        payload[3] = (byte)value;
        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MDpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(cause),
                commonAddress),
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
        public int ConnectCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public List<Iec104AsduEnvelope> Sent { get; } = new();

        public Task ConnectAsync(string host, int port, Iec104SessionOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
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
            DisconnectCount++;
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
