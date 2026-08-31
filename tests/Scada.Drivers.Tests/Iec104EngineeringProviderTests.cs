using System.Runtime.CompilerServices;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104EngineeringProviderTests
{
    [Fact]
    public void Descriptor_AnnouncesConnectionTestAndBrowseWithoutFreezingTagBindingSchema()
    {
        var provider = new Iec104EngineeringProvider(static () => new FakeAdapter(Array.Empty<Iec104AsduEnvelope>()));

        Assert.True(provider.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(provider.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.Empty(provider.Descriptor.ConfigurationSchema.TagBindingFields);
        Assert.Equal(Iec104EngineeringConnectionTester.DriverType, provider.Descriptor.DriverType);
    }

    [Fact]
    public async Task BrowseAsync_MapsObservedPointToPartialReadOnlyEngineeringNode()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(commonAddress: 1, ioa: 77, value: true, cause: 20),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        });
        var provider = new Iec104EngineeringProvider(() => adapter);

        var page = await provider.BrowseAsync(new DriverBrowseRequest(
            CreateContext(),
            Parameters: new Dictionary<string, string>
            {
                ["observationWindowSeconds"] = "1"
            }));

        Assert.True(page.IsPartial);
        Assert.Null(page.ContinuationToken);
        var node = Assert.Single(page.Nodes);
        Assert.Equal("ca=1;ioa=77", node.NodeId);
        Assert.Equal(node.NodeId, node.StableIdentity);
        Assert.Equal(node.NodeId, node.PortableAddress);
        Assert.Equal("CA 1 / IOA 77", node.DisplayName);
        Assert.True(node.IsReadable);
        Assert.False(node.IsWritable);
        Assert.False(node.IsContainer);
        Assert.Equal(TagDataType.Boolean, node.SuggestedDataType);
        Assert.Equal("1", node.Metadata!["commonAddress"]);
        Assert.Equal("77", node.Metadata["informationObjectAddress"]);
        Assert.Equal("1", node.Metadata["observedTypeIds"]);
        Assert.Equal("MSpNa1", node.Metadata["observedTypeNames"]);
        Assert.Contains(page.Issues!, static issue => issue.Code == "iec104.browse.partial");
        Assert.DoesNotContain(page.Issues!, static issue => issue.Severity == DriverEngineeringIssueSeverity.Error);
        Assert.Equal(1, adapter.ConnectCount);
        Assert.Equal(1, adapter.StartCount);
        Assert.Equal(1, adapter.StopCount);
        Assert.Equal(1, adapter.DisconnectCount);
    }

    [Fact]
    public async Task BrowseAsync_TypeConflictRequiresExplicitEngineeringReview()
    {
        var adapter = new FakeAdapter(new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateSinglePoint(commonAddress: 1, ioa: 100, value: true, cause: 20),
            CreateDoublePoint(commonAddress: 1, ioa: 100, Iec104DoublePointState.On, cause: 3),
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause)
        });
        var provider = new Iec104EngineeringProvider(() => adapter);

        var page = await provider.BrowseAsync(new DriverBrowseRequest(CreateContext()));

        var node = Assert.Single(page.Nodes);
        Assert.Null(node.SuggestedDataType);
        Assert.Equal("1,3", node.Metadata!["observedTypeIds"]);
        Assert.Contains(node.Issues!, static issue =>
            issue.Code == "iec104.browse.typeConflict" &&
            issue.Severity == DriverEngineeringIssueSeverity.Warning);
        Assert.False(node.IsWritable);
    }

    [Fact]
    public async Task BrowseAsync_ContinuationTokenIsRejectedWithoutOpeningTransport()
    {
        var factoryCalls = 0;
        var provider = new Iec104EngineeringProvider(() =>
        {
            factoryCalls++;
            return new FakeAdapter(Array.Empty<Iec104AsduEnvelope>());
        });

        var page = await provider.BrowseAsync(new DriverBrowseRequest(
            CreateContext(),
            ContinuationToken: "not-a-real-continuation"));

        Assert.Empty(page.Nodes);
        Assert.Contains(page.Issues!, static issue =>
            issue.Code == "iec104.browse.continuation.unsupported" &&
            issue.Severity == DriverEngineeringIssueSeverity.Error);
        Assert.Equal(0, factoryCalls);
    }

    private static DriverEngineeringDataSourceContext CreateContext() =>
        new(
            DataSourceKey: "iec-test",
            DataSourceName: "IEC test",
            DriverType: Iec104EngineeringConnectionTester.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "2404",
                ["commonAddresses"] = "1",
                ["stationTimeZone"] = TimeZoneInfo.Utc.Id
            },
            SecretReferences: new Dictionary<string, string>());

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

    private static Iec104AsduEnvelope CreateSinglePoint(
        ushort commonAddress,
        int ioa,
        bool value,
        byte cause)
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

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
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
