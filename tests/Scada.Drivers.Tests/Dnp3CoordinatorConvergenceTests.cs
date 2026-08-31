using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class Dnp3CoordinatorConvergenceTests
{
    [Fact]
    public void Planner_UsesV15CommunicationBindingAsCanonicalDnp3Identity()
    {
        var dataSource = DataSource("dnp3.v15");
        var tagId = Guid.NewGuid();
        var binding = Binding(
            "dnp3:analogInput:7",
            new Dictionary<string, string>
            {
                ["pointKind"] = "analogInput",
                ["index"] = "7",
                ["staticVariation"] = "G30V1",
                ["eventVariation"] = "G32V1",
                ["expectedEventClass"] = "class1"
            });
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Analog7",
                "DNP3.Analog7",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: binding.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: binding));

        var result = new Dnp3CommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, JoinIssues(result));
        var plan = Assert.IsType<Dnp3CommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        Assert.Equal(tagId, point.Tag.Id);
        Assert.Equal(binding, point.Tag.CommunicationBinding);
        Assert.Equal(Dnp3PointKind.AnalogInput, point.Binding.PointKind);
        Assert.Equal((ushort)7, point.Binding.Index);
        Assert.Equal(TagDataType.Int32, point.Binding.DataType);
        Assert.Equal(new Dnp3ObjectVariation(30, 1), point.Binding.StaticVariation);
        Assert.Equal(new Dnp3ObjectVariation(32, 1), point.Binding.EventVariation);
        Assert.Equal(Dnp3EventClass.Class1, point.Binding.ExpectedEventClass);
        Assert.False(point.Binding.Writable);
    }

    [Fact]
    public void Planner_FailsClosedForForeignSchemaTransformAndProtectedMaterial()
    {
        var dataSource = DataSource(
            "dnp3.failclosed",
            secretReferences: new Dictionary<string, string>
            {
                ["secureAuthenticationKey"] = "vault://dnp3/key"
            });
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "Unsafe",
                "DNP3.Unsafe",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: "dnp3:analogInput:0",
                ReadOnly: true,
                CommunicationBinding: new CommunicationTagBinding(
                    CommunicationTagBinding.CurrentContractVersion,
                    "foreign.driver.binding",
                    1,
                    "dnp3:analogInput:0",
                    ValueTransform: new TagPhysicalValueTransform(ByteSwap: true))));

        var result = new Dnp3CommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue => issue.Code == "DNP3_PROTECTED_MATERIAL_UNSUPPORTED" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "DNP3_TAG_BINDING_SCHEMA_MISMATCH" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "DNP3_TAG_BINDING_TRANSFORM_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public async Task Coordinator_ActivatesAfterOnlineAndStartupIntegrityWithoutFirstSample()
    {
        var dataSource = DataSource("dnp3.quiet");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "QuietAnalog",
                "DNP3.QuietAnalog",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: "dnp3:analogInput:0",
                ReadOnly: true,
                CommunicationBinding: Binding(
                    "dnp3:analogInput:0",
                    new Dictionary<string, string> { ["staticVariation"] = "G30V1" })));
        var session = new CaptureDnp3Session();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            dnp3SessionFactory: new CaptureDnp3SessionFactory(session));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-dnp3-quiet", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.False(coordinator.TryGetCurrent(tagId, out _));
        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(Dnp3DriverDescriptorProvider.DriverType, diagnostics.DriverType);
        Assert.Equal("dnp3.quiet", diagnostics.DataSourceKey);
        Assert.True(diagnostics.Counters.Connections >= 1);
        Assert.True(diagnostics.ProtocolDetails?.TryGetValue("startupIntegrityScans", out var startupScans) == true);
        Assert.Equal("1", startupScans);
    }

    [Fact]
    public async Task Coordinator_PreservesG30V1Int32AndSourceTimestampThroughCanonicalCache()
    {
        var dataSource = DataSource("dnp3.int32");
        var tagId = Guid.NewGuid();
        var sourceTimestamp = DateTimeOffset.Parse("2026-08-31T05:30:00Z");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "AnalogInt32",
                "DNP3.AnalogInt32",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: "dnp3:analogInput:0",
                ReadOnly: true,
                CommunicationBinding: Binding(
                    "dnp3:analogInput:0",
                    new Dictionary<string, string> { ["staticVariation"] = "G30V1" })));
        var session = new CaptureDnp3Session(
            new Dnp3Measurement(
                Dnp3PointKind.AnalogInput,
                0,
                4242,
                new Dnp3ObjectVariation(30, 1),
                IsEvent: false,
                Dnp3PointFlagSet.Nominal,
                sourceTimestamp));
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            dnp3SessionFactory: new CaptureDnp3SessionFactory(session));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-dnp3-int32", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.Equal(4242, Assert.IsType<int>(current!.Value));
        Assert.Equal(TagQuality.Good, current.Quality);
        Assert.Equal(sourceTimestamp, current.SourceTimestamp);
        Assert.Contains(coordinator.Tags(), tag =>
            tag.Id == tagId && tag.CommunicationBinding?.PortableAddress == "dnp3:analogInput:0");
    }

    [Fact]
    public async Task Coordinator_RoutesBinaryOutputWriteThroughActiveDnp3Session()
    {
        var dataSource = DataSource("dnp3.write");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "BreakerCommand",
                "DNP3.BreakerCommand",
                TagDataType.Boolean,
                Source: dataSource.Key,
                Address: "dnp3:binaryOutputStatus:3",
                ReadOnly: false,
                CommunicationBinding: Binding(
                    "dnp3:binaryOutputStatus:3",
                    new Dictionary<string, string>
                    {
                        ["writable"] = "true",
                        ["commandMode"] = "selectBeforeOperate",
                        ["binaryTrueOperation"] = "latchOn",
                        ["binaryFalseOperation"] = "latchOff"
                    })));
        var session = new CaptureDnp3Session();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            dnp3SessionFactory: new CaptureDnp3SessionFactory(session));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-dnp3-write", 1, package);
        Assert.True(activation.Activated, JoinIssues(activation));

        await coordinator.WriteAsync(tagId, true);

        var write = Assert.Single(session.BinaryWrites);
        Assert.Equal((ushort)3, write.Index);
        Assert.Equal(Dnp3BinaryOperation.LatchOn, write.Operation);
        Assert.Equal(Dnp3CommandMode.SelectBeforeOperate, write.Profile.Mode);
    }

    private static DataSourceEngineeringDto DataSource(
        string key,
        Dictionary<string, string>? settings = null,
        Dictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "DNP3 Runtime",
            Dnp3DriverDescriptorProvider.DriverType,
            Settings: settings ?? DefaultSettings(),
            SecretReferences: secretReferences);

    private static Dictionary<string, string> DefaultSettings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["transport"] = "tcp",
            ["host"] = "127.0.0.1",
            ["port"] = "20000",
            ["masterAddress"] = "1",
            ["outstationAddress"] = "1024",
            ["connectTimeout"] = "00:00:01",
            ["responseTimeout"] = "00:00:01",
            ["reconnectMinDelay"] = "00:00:00.100",
            ["reconnectMaxDelay"] = "00:00:01",
            ["keepAliveTimeout"] = "00:00:05",
            ["integrityPollInterval"] = "00:15:00"
        };

    private static CommunicationTagBinding Binding(
        string portableAddress,
        IReadOnlyDictionary<string, string>? settings = null) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            portableAddress,
            settings);

    private static EngineeringPackage Package(DataSourceEngineeringDto dataSource, TagEngineeringDto tag) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private static string JoinIssues(CommunicationDriverRuntimePlanningResult result) =>
        string.Join(" | ", result.Issues.Select(static issue => issue.Message));

    private static string JoinIssues(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(static issue => issue.Message)
            .Concat(result.RuntimeIssues.Select(static issue => issue.Message)));

    private sealed class CaptureDnp3SessionFactory(CaptureDnp3Session session) : IDnp3MasterSessionFactory
    {
        public IDnp3MasterSession Create(Dnp3TcpConnectionOptions connectionOptions)
        {
            connectionOptions.Validate();
            session.ConnectionOptions = connectionOptions;
            return session;
        }
    }

    private sealed class CaptureDnp3Session(Dnp3Measurement? startupMeasurement = null) : IDnp3MasterSession
    {
        private readonly object _gate = new();
        private Dnp3SessionState _state = Dnp3SessionState.Stopped;
        private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
        private long _connections;
        private long _startupIntegrityScans;

        public Dnp3TcpConnectionOptions? ConnectionOptions { get; set; }
        public List<(ushort Index, Dnp3BinaryOperation Operation, Dnp3BinaryCommandProfile Profile)> BinaryWrites { get; } = new();
        public List<(ushort Index, object Value, Dnp3AnalogCommandProfile Profile)> AnalogWrites { get; } = new();

        public Dnp3SessionState State
        {
            get
            {
                lock (_gate) return _state;
            }
        }

        public async ValueTask StartAsync(
            Dnp3AssociationOptions options,
            Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
            Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
            CancellationToken cancellationToken = default)
        {
            options.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            SetState(Dnp3SessionState.StartupIntegrity);
            await stateHandler(Dnp3SessionState.StartupIntegrity, cancellationToken);

            if (startupMeasurement is not null)
                await measurementHandler(startupMeasurement, cancellationToken);

            Interlocked.Increment(ref _startupIntegrityScans);
            Interlocked.Increment(ref _connections);
            SetState(Dnp3SessionState.Online);
            await stateHandler(Dnp3SessionState.Online, cancellationToken);
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(Dnp3SessionState.Stopped);
            await ValueTask.CompletedTask;
        }

        public ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
            ushort index,
            Dnp3BinaryOperation operation,
            Dnp3BinaryCommandProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate) BinaryWrites.Add((index, operation, profile));
            return ValueTask.FromResult(Dnp3CommandResult.Success());
        }

        public ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
            ushort index,
            object value,
            Dnp3AnalogCommandProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate) AnalogWrites.Add((index, value, profile));
            return ValueTask.FromResult(Dnp3CommandResult.Success());
        }

        public Dnp3SessionDiagnosticSnapshot GetDiagnostics()
        {
            lock (_gate)
            {
                return new Dnp3SessionDiagnosticSnapshot(
                    ConnectionOptions?.SanitizedEndpoint,
                    _state,
                    _stateChangedAt,
                    LastSuccessfulCommunicationAt: _state == Dnp3SessionState.Online ? _stateChangedAt : null,
                    Connections: Interlocked.Read(ref _connections),
                    StartupIntegrityScans: Interlocked.Read(ref _startupIntegrityScans));
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void SetState(Dnp3SessionState state)
        {
            lock (_gate)
            {
                _state = state;
                _stateChangedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}
