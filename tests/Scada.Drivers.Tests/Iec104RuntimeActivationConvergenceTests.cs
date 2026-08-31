using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Iec60870;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class Iec104RuntimeActivationConvergenceTests
{
    [Fact]
    public async Task Coordinator_ActivatesIec104ThroughSharedComponentsAndPublishesObservedPoint()
    {
        var adapter = new AutoReadyAdapter();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            iec104AdapterFactory: () => adapter);
        var compiler = new EngineeringDriverCompiler(components);
        var eventBus = new InMemoryScadaEventBus();
        await using var coordinator = new EngineeringRuntimeCoordinator(
            eventBus,
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var dataSource = CreateDataSource("iec104.runtime");
        var binding = CreateBinding("ca=1;ioa=100", "MMeNb1");
        var tagId = Guid.NewGuid();
        var tag = new TagEngineeringDto(
            tagId,
            "ScaledValue",
            "Plant.IEC104.ScaledValue",
            TagDataType.Int16,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: true,
            CommunicationBinding: binding);
        var package = CreatePackage(dataSource, tag);

        var compilation = compiler.Compile(package);

        Assert.True(compilation.CanActivate, string.Join(" | ", compilation.Issues.Select(x => x.Message)));
        Assert.Empty(compilation.ModbusTcpPlans);
        var plan = Assert.IsType<Iec104CommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));
        Assert.Equal(Iec104EngineeringConnectionTester.DriverType, plan.DriverType);
        Assert.Equal(binding.PortableAddress, Assert.Single(plan.Points).Address.ToString());

        var result = await coordinator.ActivateAsync("project-a", 1, package);

        Assert.True(result.Activated, string.Join(" | ", result.CompilationIssues.Select(x => x.Message)
            .Concat(result.RuntimeIssues.Select(x => x.Message))));
        Assert.Equal(1, adapter.ConnectCount);
        Assert.Equal(1, adapter.StartDataTransferCount);
        Assert.Equal(1, adapter.GeneralInterrogationRequestCount);
        Assert.Contains(coordinator.Tags(), active => active.Id == tagId && active.CommunicationBinding == binding);
        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.NotNull(current);
        Assert.Equal((short)1234, Assert.IsType<short>(current.Value));
        Assert.Equal(TagQuality.Good, current.Quality);
        Assert.Equal(dataSource.Key, current.Source);
        var driver = Assert.Single(coordinator.Describe().Drivers);
        Assert.Equal(dataSource.Name, driver.Name);
    }

    [Fact]
    public void Planner_FailsClosedForProtectedMaterialAndPhysicalTransformOnPlainTcpProfile()
    {
        var planner = new Iec104CommunicationRuntimePlanner();
        var dataSource = CreateDataSource(
            "iec104.invalid",
            secretReferences: new Dictionary<string, string>
            {
                ["tls.privateKey"] = "future-key"
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Iec104CommunicationRuntimePlanner.BindingSchemaId,
            Iec104CommunicationRuntimePlanner.BindingSchemaVersion,
            "ca=1;ioa=100",
            new Dictionary<string, string>
            {
                ["iec104.typeId"] = "MMeNb1"
            },
            new TagPhysicalValueTransform(ByteSwap: true));
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "InvalidPoint",
            "Plant.IEC104.InvalidPoint",
            TagDataType.Int16,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: true,
            CommunicationBinding: binding);

        var result = planner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "IEC104_PROTECTED_MATERIAL_UNSUPPORTED" && issue.IsError);
        Assert.Contains(result.Issues, issue => issue.Code == "IEC104_TAG_BINDING_TRANSFORM_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public void Planner_PreservesLegacyAddressMigrationWithExplicitWarning()
    {
        var planner = new Iec104CommunicationRuntimePlanner();
        var dataSource = CreateDataSource("iec104.legacy");
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "LegacyPoint",
            "Plant.IEC104.LegacyPoint",
            TagDataType.Int16,
            Source: dataSource.Key,
            Address: "ca=1;ioa=100",
            ReadOnly: true,
            Metadata: new Dictionary<string, string>
            {
                ["iec104.typeId"] = "MMeNb1"
            });

        var result = planner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(x => x.Message)));
        Assert.IsType<Iec104CommunicationRuntimePlan>(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "IEC104_TAG_LEGACY_BINDING" && !issue.IsError);
    }

    private static DataSourceEngineeringDto CreateDataSource(
        string key,
        IReadOnlyDictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "Runtime IEC-104",
            Iec104EngineeringConnectionTester.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "2404",
                ["commonAddresses"] = "1",
                ["stationTimeZone"] = "UTC"
            },
            SecretReferences: secretReferences);

    private static CommunicationTagBinding CreateBinding(string address, string typeId) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            Iec104CommunicationRuntimePlanner.BindingSchemaId,
            Iec104CommunicationRuntimePlanner.BindingSchemaVersion,
            address,
            new Dictionary<string, string>
            {
                ["iec104.typeId"] = typeId
            });

    private static EngineeringPackage CreatePackage(
        DataSourceEngineeringDto dataSource,
        TagEngineeringDto tag) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private sealed class AutoReadyAdapter : IIec104ClientAdapter
    {
        private readonly Channel<Iec104AsduEnvelope> _incoming = Channel.CreateUnbounded<Iec104AsduEnvelope>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int StartDataTransferCount { get; private set; }
        public int GeneralInterrogationRequestCount { get; private set; }

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("127.0.0.1", host);
            Assert.Equal(2404, port);
            options.Validate();
            ConnectCount++;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartDataTransferCount++;
            return Task.CompletedTask;
        }

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(
            Iec104AsduEnvelope asdu,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (asdu.Header.TypeId == Iec104TypeId.CIcNa1)
            {
                GeneralInterrogationRequestCount++;
                _incoming.Writer.TryWrite(CreateGeneralInterrogationResponse(
                    asdu,
                    Iec104GeneralInterrogationTransaction.ActivationConfirmationCause));
                _incoming.Writer.TryWrite(CreateScaledMonitoredValue(
                    asdu.Header.CommonAddress,
                    informationObjectAddress: 100,
                    value: 1234));
                _incoming.Writer.TryWrite(CreateGeneralInterrogationResponse(
                    asdu,
                    Iec104GeneralInterrogationTransaction.ActivationTerminationCause));
            }

            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await _incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_incoming.Reader.TryRead(out var item))
                    yield return item;
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private static Iec104AsduEnvelope CreateGeneralInterrogationResponse(
            Iec104AsduEnvelope request,
            byte cause)
        {
            var header = new Iec104AsduHeader(
                Iec104TypeId.CIcNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(
                    cause,
                    request.Header.CauseOfTransmission.OriginatorAddress),
                request.Header.CommonAddress);
            return Iec104AsduEnvelope.Create(header, request.Payload.Span);
        }

        private static Iec104AsduEnvelope CreateScaledMonitoredValue(
            ushort commonAddress,
            int informationObjectAddress,
            short value)
        {
            var payload = new byte[6];
            new Iec104InformationObjectAddress(informationObjectAddress).WriteTo(payload.AsSpan(0, 3));
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(3, 2), value);
            payload[5] = 0;

            var header = new Iec104AsduHeader(
                Iec104TypeId.MMeNb1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(20),
                commonAddress);
            return Iec104AsduEnvelope.Create(header, payload);
        }
    }
}
