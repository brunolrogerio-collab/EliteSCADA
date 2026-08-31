using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyLogixCoordinatorConvergenceTests
{
    [Fact]
    public void Planner_UsesV15CommunicationBindingAndStableTagIdentity()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Tank.Level", LogixNativeType.Dint);
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            AllenBradleyLogixContractIdentity.BindingSchemaId,
            AllenBradleyLogixContractIdentity.BindingSchemaVersion,
            address);
        var tagId = Guid.NewGuid();
        var dataSource = DataSource("clx.v15");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "TankLevel",
                "Plant.Tank.Level",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: address,
                ReadOnly: true,
                CommunicationBinding: binding));

        var result = new AllenBradleyLogixCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(static issue => issue.Message)));
        var plan = Assert.IsType<AllenBradleyLogixCommunicationRuntimePlan>(result.Plan);
        var runtimeBinding = Assert.Single(plan.Bindings);
        Assert.Equal(tagId, runtimeBinding.Tag.Id);
        Assert.Equal(binding, runtimeBinding.Tag.CommunicationBinding);
        Assert.Equal(address, runtimeBinding.PortableAddress);
        Assert.Equal(LogixExternalAccess.ReadOnly, runtimeBinding.ExternalAccess);
    }

    [Fact]
    public void Planner_PreservesLegacyMigrationWarningWithoutInventingIdentity()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Legacy.Counter", LogixNativeType.Dint);
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly);
        var dataSource = DataSource("clx.legacy");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "LegacyCounter",
                "Plant.Legacy.Counter",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: address,
                ReadOnly: true));

        var result = new AllenBradleyLogixCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(static issue => issue.Message)));
        Assert.Contains(result.Issues, static issue => issue.Code == "LOGIX_TAG_LEGACY_BINDING" && !issue.IsError);
        var plan = Assert.IsType<AllenBradleyLogixCommunicationRuntimePlan>(result.Plan);
        Assert.Null(Assert.Single(plan.Tags).CommunicationBinding);
    }

    [Fact]
    public void Planner_FailsClosedForProtectedMaterialMissingStableIdAndPhysicalTransform()
    {
        var dataSource = DataSource(
            "clx.failclosed",
            secretReferences: new Dictionary<string, string> { ["certificate"] = "future-cert-ref" });
        var firstReference = new LogixSymbolReference(LogixTagScope.Controller, "Missing.Id", LogixNativeType.Dint);
        var firstAddress = LogixPortableAddress.Format(firstReference, LogixExternalAccess.ReadOnly);
        var secondReference = new LogixSymbolReference(LogixTagScope.Controller, "Swapped.Value", LogixNativeType.Dint);
        var secondAddress = LogixPortableAddress.Format(secondReference, LogixExternalAccess.ReadOnly);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(
                    null,
                    "MissingId",
                    "Plant.MissingId",
                    TagDataType.Int32,
                    Source: dataSource.Key,
                    Address: firstAddress,
                    CommunicationBinding: Binding(firstAddress)),
                new TagEngineeringDto(
                    Guid.NewGuid(),
                    "SwappedValue",
                    "Plant.SwappedValue",
                    TagDataType.Int32,
                    Source: dataSource.Key,
                    Address: secondAddress,
                    CommunicationBinding: Binding(
                        secondAddress,
                        transform: new TagPhysicalValueTransform(ByteSwap: true)))
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var result = new AllenBradleyLogixCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue => issue.Code == "LOGIX_PROTECTED_MATERIAL_UNSUPPORTED" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "LOGIX_TAG_STABLE_ID_REQUIRED" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "LOGIX_TAG_BINDING_TRANSFORM_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public async Task Coordinator_ActivatesLogixThroughSharedComponentsAndPerformsReadWrite()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Runtime.Counter", LogixNativeType.Dint);
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadWrite);
        var dataSource = DataSource("clx.runtime");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "RuntimeCounter",
                "Plant.Runtime.Counter",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: address,
                ReadOnly: false,
                CommunicationBinding: Binding(address)));

        var client = new CaptureLogixProtocolClient();
        client.Values[reference.StableIdentity] = 42;
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            logixClientFactory: new CaptureLogixProtocolClientFactory(client));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var compilation = compiler.Compile(package);
        Assert.True(compilation.CanActivate, string.Join(" | ", compilation.Issues.Select(static issue => issue.Message)));
        Assert.IsType<AllenBradleyLogixCommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));

        var activation = await coordinator.ActivateAsync("project-logix", 1, package);

        Assert.True(activation.Activated, string.Join(" | ", activation.CompilationIssues.Select(static issue => issue.Message)
            .Concat(activation.RuntimeIssues.Select(static issue => issue.Message))));
        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.Equal(42, Assert.IsType<int>(current!.Value));
        Assert.Equal(TagQuality.Good, current.Quality);
        Assert.Contains(coordinator.Tags(), tag => tag.Id == tagId && tag.CommunicationBinding == Binding(address));
        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(AllenBradleyLogixContractIdentity.DriverType, diagnostics.DriverType);
        Assert.True(diagnostics.Counters.ReadOperations >= 1);

        await coordinator.WriteAsync(tagId, 77);

        Assert.Equal(77, Assert.IsType<int>(client.Values[reference.StableIdentity]));
        var write = Assert.Single(client.Writes);
        Assert.Equal(reference.StableIdentity, write.Reference.StableIdentity);
        Assert.Equal(77, Assert.IsType<int>(write.Value));
        Assert.True(coordinator.TryGetCurrent(tagId, out var written));
        Assert.Equal(77, Assert.IsType<int>(written!.Value));
    }

    [Fact]
    public async Task Coordinator_TreatsPointLocalLogixFailureAsReadySourceAfterBoundedAcquisition()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Missing.Symbol", LogixNativeType.Dint);
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly);
        var dataSource = DataSource("clx.degraded");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "MissingSymbol",
                "Plant.Missing.Symbol",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: address,
                ReadOnly: true,
                CommunicationBinding: Binding(address)));

        var client = new CaptureLogixProtocolClient();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            logixClientFactory: new CaptureLogixProtocolClientFactory(client));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-logix-degraded", 1, package);

        Assert.True(activation.Activated, string.Join(" | ", activation.CompilationIssues.Select(static issue => issue.Message)
            .Concat(activation.RuntimeIssues.Select(static issue => issue.Message))));
        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.Equal(TagQuality.BadConfiguration, current!.Quality);
        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        Assert.True(diagnostics.Counters.ReadOperations >= 1);
    }

    [Fact]
    public async Task HostAdapter_DoesNotBindRuntimeLifetimeToStartupCallerToken()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Lifetime.Counter", LogixNativeType.Dint);
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly);
        var dataSource = DataSource("clx.lifetime");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "LifetimeCounter",
                "Plant.Lifetime.Counter",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: address,
                ReadOnly: true,
                CommunicationBinding: Binding(address)));
        var plannerResult = new AllenBradleyLogixCommunicationRuntimePlanner().Plan(package, dataSource);
        var plan = Assert.IsType<AllenBradleyLogixCommunicationRuntimePlan>(plannerResult.Plan);
        var client = new CaptureLogixProtocolClient();
        client.Values[reference.StableIdentity] = 1;
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var factory = new AllenBradleyLogixCommunicationRuntimeFactory(new CaptureLogixProtocolClientFactory(client));
        await using var driver = factory.Create(
            plan,
            new CommunicationDriverRuntimeServices("project-lifetime", cache, registry));
        using var startup = new CancellationTokenSource();

        await driver.StartAsync(startup.Token);
        await client.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(1));
        startup.Cancel();
        await client.SecondRead.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(client.ReadManyCalls >= 2);
        Assert.IsAssignableFrom<ICommunicationDriverReadinessSource>(driver);
        Assert.Equal(CommunicationDriverReadinessState.Ready,
            ((ICommunicationDriverReadinessSource)driver).GetCommunicationReadiness().State);
        await driver.StopAsync();
    }

    private static DataSourceEngineeringDto DataSource(
        string key,
        Dictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "Logix Runtime",
            AllenBradleyLogixContractIdentity.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "44818",
                ["profile"] = "CompactLogix",
                ["scanIntervalMs"] = "50",
                ["requestTimeoutMs"] = "1000",
                ["reconnectMinimumMs"] = "50",
                ["reconnectMaximumMs"] = "500",
                ["maxBatchSize"] = "16",
                ["securityMode"] = "Unsecured"
            },
            SecretReferences: secretReferences);

    private static CommunicationTagBinding Binding(
        string address,
        TagPhysicalValueTransform? transform = null) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            AllenBradleyLogixContractIdentity.BindingSchemaId,
            AllenBradleyLogixContractIdentity.BindingSchemaVersion,
            address,
            ValueTransform: transform);

    private static EngineeringPackage Package(DataSourceEngineeringDto dataSource, TagEngineeringDto tag) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private sealed class CaptureLogixProtocolClientFactory(CaptureLogixProtocolClient client) : ILogixProtocolClientFactory
    {
        public ILogixProtocolClient Create() => client;
    }

    private sealed class CaptureLogixProtocolClient : ILogixProtocolClient
    {
        private long _requests;
        private long _connections;
        private long _disconnections;
        private int _readManyCalls;

        public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(LogixSymbolReference Reference, object Value)> Writes { get; } = new();
        public bool IsConnected { get; private set; }
        public int ReadManyCalls => Volatile.Read(ref _readManyCalls);
        public TaskCompletionSource FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask ConnectAsync(AllenBradleyLogixOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.Validate();
            if (!IsConnected) Interlocked.Increment(ref _connections);
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsConnected) Interlocked.Increment(ref _disconnections);
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LogixControllerIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LogixControllerIdentity(1, 14, 1, 35, 11, 0x12345678, "Fake Logix"));
        }

        public ValueTask<IReadOnlyList<LogixReadResult>> ReadManyAsync(
            IReadOnlyList<LogixSymbolReference> references,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Add(ref _requests, references.Count);
            var call = Interlocked.Increment(ref _readManyCalls);
            if (call == 1) FirstRead.TrySetResult();
            if (call == 2) SecondRead.TrySetResult();
            IReadOnlyList<LogixReadResult> results = references
                .Select(reference => Values.TryGetValue(reference.StableIdentity, out var value)
                    ? new LogixReadResult(reference, true, value)
                    : new LogixReadResult(reference, false, Error: LogixProtocolError.SymbolNotFound, Message: "missing"))
                .ToArray();
            return ValueTask.FromResult(results);
        }

        public ValueTask<LogixSymbolBrowsePage> BrowseControllerSymbolsAsync(
            uint startInstance = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LogixSymbolBrowsePage(Array.Empty<LogixBrowseSymbol>(), null, false));
        }

        public ValueTask WriteAsync(
            LogixSymbolReference reference,
            object? nativeValue,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nativeValue is null) throw new ArgumentNullException(nameof(nativeValue));
            Interlocked.Increment(ref _requests);
            Values[reference.StableIdentity] = nativeValue;
            Writes.Add((reference, nativeValue));
            return ValueTask.CompletedTask;
        }

        public LogixTransportDiagnosticSnapshot GetDiagnostics() => new(
            IsConnected,
            Interlocked.Read(ref _requests),
            Interlocked.Read(ref _requests),
            0,
            0,
            Interlocked.Read(ref _connections),
            Interlocked.Read(ref _disconnections),
            0,
            Interlocked.Read(ref _connections) > 0 ? DateTimeOffset.UtcNow : null,
            Interlocked.Read(ref _disconnections) > 0 ? DateTimeOffset.UtcNow : null,
            null);

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
