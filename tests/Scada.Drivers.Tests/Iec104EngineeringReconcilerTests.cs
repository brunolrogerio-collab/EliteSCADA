using System.Runtime.CompilerServices;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104EngineeringReconcilerTests
{
    [Fact]
    public void Descriptor_AnnouncesReconcileWithoutFreezingRichBindingSchema()
    {
        var reconciler = new Iec104EngineeringReconciler(static () => new FakeAdapter(Array.Empty<Iec104AsduEnvelope>()));

        Assert.True(reconciler.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Reconcile));
        Assert.True(reconciler.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.Empty(reconciler.Descriptor.ConfigurationSchema.TagBindingFields);
    }

    [Fact]
    public async Task CompletedGi_ReportsObservedPointUnchangedAndAbsentPointMissing()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(commonAddress: 1, ioa: 77, value: true, cause: 20),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        });
        var reconciler = new Iec104EngineeringReconciler(() => adapter);

        var results = await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            CreateContext(),
            new[] { "ca=1;ioa=77", "ca=1;ioa=99" })));

        Assert.Equal(2, results.Count);

        var unchanged = results.Single(result => result.PortableAddress == "ca=1;ioa=77");
        Assert.Equal(DriverReconcileStatus.Unchanged, unchanged.Status);
        Assert.Equal("ca=1;ioa=77", unchanged.ResolvedIdentity);
        Assert.Equal("ca=1;ioa=77", unchanged.ResolvedPortableAddress);
        Assert.Equal(TagDataType.Boolean, unchanged.ObservedDataType);
        Assert.True(unchanged.IsReadable);
        Assert.False(unchanged.IsWritable);
        Assert.Equal("boundedGiObservation", unchanged.Metadata!["reconcileEvidence"]);

        var missing = results.Single(result => result.PortableAddress == "ca=1;ioa=99");
        Assert.Equal(DriverReconcileStatus.Missing, missing.Status);
        Assert.Equal("ca=1;ioa=99", missing.ResolvedPortableAddress);
        Assert.Contains(missing.Issues!, static issue => issue.Code == "iec104.reconcile.missing");
    }

    [Fact]
    public async Task IncompleteGi_ReportsAbsentPointAmbiguousNotMissing()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause)
        });
        var reconciler = new Iec104EngineeringReconciler(() => adapter);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            CreateContext(),
            new[] { "ca=1;ioa=500" }))));

        Assert.Equal(DriverReconcileStatus.Ambiguous, result.Status);
        Assert.Contains(result.Issues!, static issue => issue.Code == "iec104.browse.gi.incomplete");
    }

    [Fact]
    public async Task TypeConflict_IsAmbiguousAndDoesNotInventObservedDataType()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(commonAddress: 1, ioa: 100, value: true, cause: 20),
            CreateDoublePoint(commonAddress: 1, ioa: 100, Iec104DoublePointState.On, cause: 3),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        });
        var reconciler = new Iec104EngineeringReconciler(() => adapter);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            CreateContext(),
            new[] { "ca=1;ioa=100" }))));

        Assert.Equal(DriverReconcileStatus.Ambiguous, result.Status);
        Assert.Null(result.ObservedDataType);
        Assert.Contains(result.Issues!, static issue => issue.Code == "iec104.browse.typeConflict");
        Assert.Equal("1,3", result.Metadata!["observedTypeIds"]);
    }

    [Fact]
    public async Task CommonAddressOutsideDataSourceProfile_IsUnsupported()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        });
        var reconciler = new Iec104EngineeringReconciler(() => adapter);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            CreateContext(),
            new[] { "ca=2;ioa=10" }))));

        Assert.Equal(DriverReconcileStatus.Unsupported, result.Status);
        Assert.Contains(result.Issues!, static issue => issue.Code == "iec104.reconcile.commonAddress.unconfigured");
    }

    private static async Task<List<DriverReconcileResult>> CollectAsync(IAsyncEnumerable<DriverReconcileResult> source)
    {
        var results = new List<DriverReconcileResult>();
        await foreach (var result in source)
            results.Add(result);
        return results;
    }

    private static DriverEngineeringDataSourceContext CreateContext() =>
        new(
            DataSourceKey: "iec-reconcile",
            DataSourceName: "IEC reconcile",
            DriverType: Iec104EngineeringConnectionTester.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "2404",
                ["commonAddresses"] = "1",
                ["stationTimeZone"] = TimeZoneInfo.Utc.Id
            },
            SecretReferences: new Dictionary<string, string>());

    private static Iec104AsduEnvelope CreateGiResponse(byte cause) =>
        Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.CIcNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(cause),
                CommonAddress: 1),
            new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi });

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

        public Task ConnectAsync(string host, int port, Iec104SessionOptions options, CancellationToken cancellationToken = default)
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
