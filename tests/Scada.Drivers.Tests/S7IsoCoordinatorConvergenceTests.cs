using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class S7IsoCoordinatorConvergenceTests
{
    [Fact]
    public void Planner_UsesV15CommunicationBindingAndSharedPhysicalTransform()
    {
        var dataSource = DataSource("s7.v15", port: 102);
        var tagId = Guid.NewGuid();
        var communicationBinding = Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            12,
            S7IsoValueType.Float32,
            DbNumber: 7,
            Writable: true,
            ValueOrder: S7IsoValueOrder.WordSwap));
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Pressure",
                "S7.DB7.Pressure",
                TagDataType.Float,
                Source: dataSource.Key,
                Address: communicationBinding.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: communicationBinding));

        var result = new S7IsoCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, JoinIssues(result));
        var plan = Assert.IsType<S7IsoCommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        Assert.Equal(tagId, point.Tag.Id);
        Assert.Equal(communicationBinding, point.Tag.CommunicationBinding);
        Assert.Equal(S7IsoArea.DataBlock, point.Area);
        Assert.Equal((ushort)7, point.DbNumber);
        Assert.Equal(12, point.ByteOffset);
        Assert.Equal(S7IsoValueType.Float32, point.ValueType);
        Assert.Equal(S7IsoValueOrder.WordSwap, point.ValueOrder);
        Assert.True(point.Writable);
    }

    [Fact]
    public void Planner_FailsClosedForProtectedMaterialForeignSchemaAndInvalidTransform()
    {
        var dataSource = DataSource(
            "s7.failclosed",
            port: 102,
            secretReferences: new Dictionary<string, string>
            {
                ["password"] = "vault://s7/password"
            });
        const string portable =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int16;string=0;writable=false";
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "Unsafe",
                "S7.Unsafe",
                TagDataType.Int16,
                Source: dataSource.Key,
                Address: portable,
                ReadOnly: true,
                CommunicationBinding: new CommunicationTagBinding(
                    CommunicationTagBinding.CurrentContractVersion,
                    "foreign.s7.binding",
                    1,
                    portable,
                    ValueTransform: new TagPhysicalValueTransform(WordSwap: true))));

        var result = new S7IsoCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue => issue.Code == "S7_PROTECTED_MATERIAL_UNSUPPORTED" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "S7_TAG_BINDING_SCHEMA_MISMATCH" && issue.IsError);

        var transformOnly = new TagEngineeringDto(
            Guid.NewGuid(),
            "BadTransform",
            "S7.BadTransform",
            TagDataType.Int16,
            Source: dataSource.Key,
            Address: portable,
            ReadOnly: true,
            CommunicationBinding: new CommunicationTagBinding(
                CommunicationTagBinding.CurrentContractVersion,
                S7IsoCommunicationBindingProjection.SchemaId,
                S7IsoCommunicationBindingProjection.SchemaVersion,
                portable,
                ValueTransform: new TagPhysicalValueTransform(WordSwap: true)));
        var noSecretDataSource = DataSource("s7.badtransform", port: 102);
        var transformResult = new S7IsoCommunicationRuntimePlanner().Plan(
            Package(noSecretDataSource, transformOnly with { Source = noSecretDataSource.Key }),
            noSecretDataSource);

        Assert.False(transformResult.CanActivate);
        Assert.Contains(transformResult.Issues, static issue => issue.Code == "S7_TAG_BINDING_INVALID" && issue.IsError);
    }

    [Fact]
    public async Task Coordinator_ActivatesAgainstIsoServerNegotiatesPduReadsWritesAndUpdatesCache()
    {
        await using var server = new TestS7IsoServer(240);
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x12, 0x34 });

        var dataSource = DataSource("s7.runtime", server.Port, writeEnabled: true);
        var tagId = Guid.NewGuid();
        var communicationBinding = Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            Writable: true));
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Setpoint",
                "S7.Setpoint",
                TagDataType.Int16,
                Source: dataSource.Key,
                Address: communicationBinding.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: communicationBinding));
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(3),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-s7-runtime", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.True(coordinator.TryGetCurrent(tagId, out var initial));
        Assert.Equal((short)0x1234, Assert.IsType<short>(initial!.Value));
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Contains(coordinator.Tags(), tag =>
            tag.Id == tagId && tag.CommunicationBinding?.PortableAddress == communicationBinding.PortableAddress);

        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(S7IsoCommunicationRuntimePlan.DriverTypeKey, diagnostics.DriverType);
        Assert.Equal(dataSource.Key, diagnostics.DataSourceKey);
        Assert.True(diagnostics.Counters.Connections >= 1);
        var details = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(diagnostics.ProtocolDetails);
        Assert.Equal("240", details["negotiatedPduSize"]);
        Assert.Equal("true", details["writeEnabled"]);
        Assert.Equal("1", details["lastReadPointCount"]);

        await coordinator.WriteAsync(tagId, (short)0x4567);

        Assert.Equal(new byte[] { 0x45, 0x67 }, server.GetBytes(S7IsoArea.Merker, 0, 0, 2));
        Assert.True(coordinator.TryGetCurrent(tagId, out var afterWrite));
        Assert.Equal((short)0x4567, Assert.IsType<short>(afterWrite!.Value));
        Assert.Equal(TagQuality.Good, afterWrite.Quality);
    }

    [Fact]
    public async Task Coordinator_TreatsPointLocalPduFailureAsReadySourceAfterInitialAcquisition()
    {
        await using var server = new TestS7IsoServer(240);
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x00, 0x2A });

        var dataSource = DataSource("s7.degraded", server.Port);
        var goodId = Guid.NewGuid();
        var oversizedId = Guid.NewGuid();
        var goodBinding = Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16));
        var oversizedBinding = Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            100,
            S7IsoValueType.String,
            StringLength: 254));
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                goodId,
                "GoodPoint",
                "S7.GoodPoint",
                TagDataType.Int16,
                Source: dataSource.Key,
                Address: goodBinding.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: goodBinding),
            new TagEngineeringDto(
                oversizedId,
                "OversizedPoint",
                "S7.OversizedPoint",
                TagDataType.String,
                Source: dataSource.Key,
                Address: oversizedBinding.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: oversizedBinding));
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(3),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-s7-degraded", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.True(coordinator.TryGetCurrent(goodId, out var good));
        Assert.Equal((short)42, Assert.IsType<short>(good!.Value));
        Assert.Equal(TagQuality.Good, good.Quality);
        Assert.True(coordinator.TryGetCurrent(oversizedId, out var oversized));
        Assert.Equal(TagQuality.BadConfiguration, oversized!.Quality);

        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        var details = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(diagnostics.ProtocolDetails);
        Assert.Equal("240", details["negotiatedPduSize"]);
    }

    private static DataSourceEngineeringDto DataSource(
        string key,
        int port,
        bool writeEnabled = false,
        Dictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "Siemens S7 Runtime",
            S7IsoCommunicationRuntimePlan.DriverTypeKey,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["cpuFamily"] = nameof(S7CpuFamily.S71500),
                ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
                ["rack"] = "0",
                ["slot"] = "1",
                ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
                ["writeEnabled"] = writeEnabled ? "true" : "false",
                ["sourceTsap"] = "0x0100",
                ["connectTimeoutMs"] = "500",
                ["requestTimeoutMs"] = "250",
                ["reconnectDelayMs"] = "50",
                ["requestedPduSize"] = "480"
            },
            SecretReferences: secretReferences);

    private static CommunicationTagBinding Binding(S7IsoTagBinding binding)
    {
        var transform = S7IsoCommunicationBindingProjection.GetPhysicalValueTransform(binding);
        return new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding),
            new TagPhysicalValueTransform(
                ByteSwap: transform.ByteSwap,
                WordSwap: transform.WordSwap));
    }

    private static EngineeringPackage Package(
        DataSourceEngineeringDto dataSource,
        params TagEngineeringDto[] tags) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private static string JoinIssues(CommunicationDriverRuntimePlanningResult result) =>
        string.Join(" | ", result.Issues.Select(static issue => $"{issue.Code}: {issue.Message}"));

    private static string JoinIssues(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(static issue => $"{issue.Code}: {issue.Message}")
            .Concat(result.RuntimeIssues.Select(static issue => $"{issue.Code}: {issue.Message}")));
}
