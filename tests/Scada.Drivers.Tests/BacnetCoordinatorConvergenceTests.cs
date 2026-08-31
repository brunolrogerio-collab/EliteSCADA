using System.IO.BACnet;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class BacnetCoordinatorConvergenceTests
{
    [Fact]
    public void Planner_UsesV15CommunicationBindingWithCovAndWritePriorityInSettings()
    {
        var dataSource = DataSource("bacnet.v15", 1201);
        var tagId = Guid.NewGuid();
        var protocolBinding = new BacnetBinding(
            1201,
            ObjectType: 0,
            ObjectInstance: 37,
            PropertyIdentifier: 85,
            UseCov: true,
            WritePriority: 8);
        var communicationBinding = Binding(protocolBinding);
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Temperature",
                "BACnet.AI37.Temperature",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: communicationBinding.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: communicationBinding));

        var result = new BacnetCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, JoinIssues(result));
        var plan = Assert.IsType<BacnetCommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        Assert.Equal(tagId, point.Tag.Id);
        Assert.Equal(communicationBinding, point.Tag.CommunicationBinding);
        Assert.Equal(protocolBinding.PortableAddress, point.Binding.PortableAddress);
        Assert.True(point.Binding.UseCov);
        Assert.Equal((byte)8, point.Binding.WritePriority);
        Assert.Equal("true", communicationBinding.EffectiveSettings["useCov"]);
        Assert.Equal("8", communicationBinding.EffectiveSettings["writePriority"]);
        Assert.DoesNotContain("useCov", communicationBinding.PortableAddress, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("priority", communicationBinding.PortableAddress, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_FailsClosedForProtectedMaterialForeignSchemaPhysicalTransformAndDeviceMismatch()
    {
        var protocolBinding = new BacnetBinding(1201, 0, 37, 85, UseCov: false);
        var canonical = Binding(protocolBinding);
        var protectedDataSource = DataSource(
            "bacnet.failclosed",
            1201,
            secretReferences: new Dictionary<string, string>
            {
                ["password"] = "vault://bacnet/password"
            });
        var foreignBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "foreign.bacnet.binding",
            BacnetCommunicationBindingProjection.SchemaVersion,
            canonical.PortableAddress,
            canonical.EffectiveSettings);
        var protectedResult = new BacnetCommunicationRuntimePlanner().Plan(
            Package(
                protectedDataSource,
                new TagEngineeringDto(
                    Guid.NewGuid(),
                    "Unsafe",
                    "BACnet.Unsafe",
                    TagDataType.Double,
                    Source: protectedDataSource.Key,
                    Address: foreignBinding.PortableAddress,
                    ReadOnly: true,
                    CommunicationBinding: foreignBinding)),
            protectedDataSource);

        Assert.False(protectedResult.CanActivate);
        Assert.Null(protectedResult.Plan);
        Assert.Contains(protectedResult.Issues, static issue => issue.Code == "BACNET_PROTECTED_MATERIAL_UNSUPPORTED" && issue.IsError);
        Assert.Contains(protectedResult.Issues, static issue => issue.Code == "BACNET_TAG_BINDING_SCHEMA_MISMATCH" && issue.IsError);

        var transformDataSource = DataSource("bacnet.transform", 1201);
        var transformed = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            BacnetCommunicationBindingProjection.SchemaId,
            BacnetCommunicationBindingProjection.SchemaVersion,
            canonical.PortableAddress,
            canonical.EffectiveSettings,
            new TagPhysicalValueTransform(ByteSwap: true));
        var transformResult = new BacnetCommunicationRuntimePlanner().Plan(
            Package(
                transformDataSource,
                new TagEngineeringDto(
                    Guid.NewGuid(),
                    "BadTransform",
                    "BACnet.BadTransform",
                    TagDataType.Double,
                    Source: transformDataSource.Key,
                    Address: transformed.PortableAddress,
                    ReadOnly: true,
                    CommunicationBinding: transformed)),
            transformDataSource);

        Assert.False(transformResult.CanActivate);
        Assert.Contains(transformResult.Issues, static issue => issue.Code == "BACNET_TAG_PHYSICAL_TRANSFORM_UNSUPPORTED" && issue.IsError);

        var mismatchDataSource = DataSource("bacnet.mismatch", 2202);
        var mismatchResult = new BacnetCommunicationRuntimePlanner().Plan(
            Package(
                mismatchDataSource,
                new TagEngineeringDto(
                    Guid.NewGuid(),
                    "WrongDevice",
                    "BACnet.WrongDevice",
                    TagDataType.Double,
                    Source: mismatchDataSource.Key,
                    Address: canonical.PortableAddress,
                    ReadOnly: true,
                    CommunicationBinding: canonical)),
            mismatchDataSource);

        Assert.False(mismatchResult.CanActivate);
        Assert.Contains(mismatchResult.Issues, static issue => issue.Code == "BACNET_TAG_DEVICE_MISMATCH" && issue.IsError);
    }

    [Fact]
    public async Task Coordinator_ActivatesReadsWritesAndFallsBackToPollingWhenInitialCovSubscriptionIsUnavailable()
    {
        var protocolBinding = new BacnetBinding(
            1201,
            ObjectType: 0,
            ObjectInstance: 37,
            PropertyIdentifier: 85,
            UseCov: true,
            WritePriority: 8);
        var communicationBinding = Binding(protocolBinding);
        var dataSource = DataSource("bacnet.runtime", 1201);
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Temperature",
                "BACnet.Runtime.Temperature",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: communicationBinding.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: communicationBinding));
        var session = new SequencedBacnetSession(
            subscribeAvailable: false,
            ReadStep.Success(41.25f));
        var sessions = new TrackingSessionFactory(session);
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(bacnetSessionFactory: sessions);
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(3),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-bacnet-runtime", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.Equal(1, sessions.CreateCalls);
        Assert.NotNull(sessions.LastOptions);
        Assert.Equal(1, session.SubscribeCalls);
        Assert.True(session.ReadCalls >= 1);
        Assert.True(coordinator.TryGetCurrent(tagId, out var initial));
        Assert.Equal(41.25d, Assert.IsType<double>(initial!.Value), precision: 6);
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Contains(coordinator.Tags(), tag =>
            tag.Id == tagId && tag.CommunicationBinding?.PortableAddress == communicationBinding.PortableAddress);

        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(BacnetDriverDescriptor.DriverType, diagnostics.DriverType);
        Assert.Equal(dataSource.Key, diagnostics.DataSourceKey);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
        var details = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(diagnostics.ProtocolDetails);
        Assert.Equal("1201", details["deviceInstance"]);
        Assert.Equal("true", details["deviceReachable"]);
        Assert.Equal("0", details["covTagCount"]);
        Assert.Equal("1", details["polledTagCount"]);

        await coordinator.WriteAsync(tagId, 43.75d);

        Assert.Equal(1, session.WriteCalls);
        Assert.NotNull(session.LastWriteBinding);
        Assert.Equal((byte)8, session.LastWriteBinding!.WritePriority);
        var written = Assert.Single(session.LastWriteValues!);
        Assert.Equal(43.75d, Assert.IsType<double>(written.Value), precision: 6);

        using var convergenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(
            () => coordinator.TryGetCurrent(tagId, out var current) &&
                  current?.Value is double value &&
                  Math.Abs(value - 43.75d) < 0.000001d,
            convergenceTimeout.Token);

        Assert.True(coordinator.TryGetCurrent(tagId, out var afterWrite));
        Assert.Equal(43.75d, Assert.IsType<double>(afterWrite!.Value), precision: 6);
        Assert.Equal(TagQuality.Good, afterWrite.Quality);
    }

    [Fact]
    public async Task Coordinator_RecoversFromInitialTimeoutWithinActivationReadinessWindow()
    {
        var protocolBinding = new BacnetBinding(
            2202,
            ObjectType: 0,
            ObjectInstance: 7,
            PropertyIdentifier: 85,
            UseCov: false);
        var communicationBinding = Binding(protocolBinding);
        var dataSource = DataSource("bacnet.recovery", 2202, scanIntervalMilliseconds: 50);
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "Pressure",
                "BACnet.Recovery.Pressure",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: communicationBinding.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: communicationBinding));
        var session = new SequencedBacnetSession(
            subscribeAvailable: false,
            ReadStep.Failure(new TimeoutException("initial BACnet timeout")),
            ReadStep.Success(55.5f));
        var sessions = new TrackingSessionFactory(session);
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(bacnetSessionFactory: sessions);
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(3),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-bacnet-recovery", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.True(session.ReadCalls >= 2);
        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.Equal(55.5d, Assert.IsType<double>(current!.Value), precision: 6);
        Assert.Equal(TagQuality.Good, current.Quality);

        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
        Assert.Equal(1, diagnostics.Counters.Timeouts);
        Assert.Equal(1, diagnostics.Counters.Connections);
        Assert.Equal(0, diagnostics.Counters.Reconnects);
        Assert.Equal("true", diagnostics.ProtocolDetails!["deviceReachable"]);
    }

    private static DataSourceEngineeringDto DataSource(
        string key,
        uint deviceInstance,
        int scanIntervalMilliseconds = 50,
        Dictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "BACnet/IP Runtime",
            BacnetDriverDescriptor.DriverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["deviceInstance"] = deviceInstance.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["localPort"] = "47808",
                ["scanIntervalMilliseconds"] = scanIntervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["requestTimeoutMilliseconds"] = "250",
                ["discoveryWindowMilliseconds"] = "250"
            },
            SecretReferences: secretReferences);

    private static CommunicationTagBinding Binding(BacnetBinding binding) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            BacnetCommunicationBindingProjection.SchemaId,
            BacnetCommunicationBindingProjection.SchemaVersion,
            BacnetCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            BacnetCommunicationBindingProjection.ToCanonicalSettings(binding));

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

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private sealed record ReadStep(float? Value, Exception? Error)
    {
        public static ReadStep Success(float value) => new(value, null);
        public static ReadStep Failure(Exception error) => new(null, error);
    }

    private sealed class TrackingSessionFactory(SequencedBacnetSession session) : IBacnetSessionFactory
    {
        public int CreateCalls { get; private set; }
        public BacnetSessionOptions? LastOptions { get; private set; }

        public IBacnetSession Create(BacnetSessionOptions options)
        {
            CreateCalls++;
            LastOptions = options;
            return session;
        }
    }

    private sealed class SequencedBacnetSession(bool subscribeAvailable, params ReadStep[] steps) : IBacnetSession
    {
        private readonly object _stepsGate = new();
        private int _readIndex = -1;
        private int _subscribeCalls;
        private int _writeCalls;

        public int ReadCalls => Volatile.Read(ref _readIndex) + 1;
        public int SubscribeCalls => Volatile.Read(ref _subscribeCalls);
        public int WriteCalls => Volatile.Read(ref _writeCalls);
        public BacnetBinding? LastWriteBinding { get; private set; }
        public IReadOnlyCollection<BacnetValue>? LastWriteValues { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<BacnetDeviceObservation> ResolveDeviceAsync(
            uint deviceInstance,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(
            int? maximumResults = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BacnetPropertyReadResult> ReadAsync(
            BacnetBinding binding,
            CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _readIndex);
            ReadStep step;
            lock (_stepsGate)
                step = steps[Math.Min(index, steps.Length - 1)];
            if (step.Error is not null)
                return Task.FromException<BacnetPropertyReadResult>(step.Error);

            return Task.FromResult(new BacnetPropertyReadResult(
                binding,
                [new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, step.Value!.Value)],
                DateTimeOffset.UtcNow,
                new BacnetObjectState(Reliability: 0),
                UsedReadPropertyMultiple: true));
        }

        public Task WriteAsync(
            BacnetBinding binding,
            IReadOnlyCollection<BacnetValue> values,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeCalls);
            LastWriteBinding = binding;
            var writtenValues = values.ToArray();
            LastWriteValues = writtenValues;

            // This fake models a device that accepted the write. Subsequent polls must
            // therefore read the accepted value instead of permanently replaying the
            // pre-write sample and racing the coordinator's write publication.
            if (writtenValues.Length == 1 && writtenValues[0].Value is not null)
            {
                var reflectedValue = Convert.ToSingle(
                    writtenValues[0].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                lock (_stepsGate)
                    steps[^1] = ReadStep.Success(reflectedValue);
            }

            return Task.CompletedTask;
        }

        public Task<IDisposable?> TrySubscribeCovAsync(
            BacnetBinding binding,
            Func<BacnetPropertyReadResult, ValueTask> onNotification,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _subscribeCalls);
            return Task.FromResult<IDisposable?>(subscribeAvailable ? new SubscriptionHandle() : null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class SubscriptionHandle : IDisposable
        {
            public void Dispose() { }
        }
    }
}
