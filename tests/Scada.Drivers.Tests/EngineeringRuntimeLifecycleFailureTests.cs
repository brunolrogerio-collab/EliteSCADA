using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeLifecycleFailureTests
{
    private const string DriverType = "test.lifecycle-probe";
    private const string SourceKey = "lifecycle.source";

    [Fact]
    public async Task ActivateAsync_RestartsResourceOwnerWhenStopFailsAfterPartialShutdown()
    {
        var state = new LifecycleProbeState();
        var components = BuildComponents(state);
        await using var runtime = CreateRuntime(components);
        var package = BuildPackage(resourceOwner: true);

        var initial = await runtime.ActivateAsync("lifecycle-project", 1, package);
        Assert.True(initial.Activated, string.Join(" | ", initial.RuntimeIssues.Select(issue => issue.Message)));
        var previousDriver = Assert.Single(state.Drivers);
        Assert.True(previousDriver.IsRunning);

        state.ThrowOnNextStop = 1;
        var failedHandover = await runtime.ActivateAsync("lifecycle-project", 2, package);

        Assert.False(failedHandover.Activated);
        Assert.Equal(2, previousDriver.StartCalls);
        Assert.True(previousDriver.IsRunning);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.Equal(DriverState.Running, Assert.Single(runtime.Describe().Drivers).State);
        Assert.Equal(2, state.Drivers.Count);
        Assert.Equal(0, state.Drivers[1].StartCalls);
        Assert.Equal(1, state.Drivers[1].DisposeCalls);
    }

    [Fact]
    public async Task DisposeAsync_DisposesDriverEvenWhenStopFails()
    {
        var state = new LifecycleProbeState();
        var components = BuildComponents(state);
        var runtime = CreateRuntime(components);
        var result = await runtime.MaterializePassiveAsync(
            "lifecycle-project",
            1,
            BuildPackage(resourceOwner: false),
            DateTimeOffset.UtcNow);
        Assert.True(result.Activated, string.Join(" | ", result.RuntimeIssues.Select(issue => issue.Message)));
        var driver = Assert.Single(state.Drivers);

        state.ThrowOnNextStop = 1;
        await Assert.ThrowsAsync<AggregateException>(async () => await runtime.DisposeAsync());

        Assert.Equal(0, driver.StartCalls);
        Assert.Equal(1, driver.StopCalls);
        Assert.Equal(1, driver.DisposeCalls);
    }

    [Fact]
    public async Task ActivateAsync_ReportsWhenHandoverRollbackCannotRestartPreviousDriver()
    {
        var state = new LifecycleProbeState();
        var components = BuildComponents(state);
        await using var runtime = CreateRuntime(components);
        var package = BuildPackage(resourceOwner: true);

        Assert.True((await runtime.ActivateAsync("lifecycle-project", 1, package)).Activated);
        state.ThrowOnNextStop = 1;
        state.ThrowOnNextStart = 1;

        var failedHandover = await runtime.ActivateAsync("lifecycle-project", 2, package);

        Assert.False(failedHandover.Activated);
        Assert.Contains(
            failedHandover.RuntimeIssues,
            issue => issue.Code == "RUNTIME_ACTIVATION_FAILED" &&
                     issue.Message.Contains("could not be restarted", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DriverState.Stopped, Assert.Single(runtime.Describe().Drivers).State);
    }

    private static EngineeringRuntimeCoordinator CreateRuntime(
        CommunicationDriverRuntimeComponentRegistry components) =>
        new(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(1),
            communicationComponents: components);

    private static CommunicationDriverRuntimeComponentRegistry BuildComponents(LifecycleProbeState state)
    {
        var components = new CommunicationDriverRuntimeComponentRegistry();
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new LifecycleProbePlanner(),
            new LifecycleProbeFactory(state),
            new CommunicationDriverTypeDescriptor(
                DriverType,
                "Lifecycle Probe",
                DriverContractVersion: 1,
                RuntimeCapabilities: DriverCapabilities.Read,
                EngineeringCapabilities: DriverEngineeringCapabilities.None,
                AcquisitionModes: [DriverAcquisitionMode.Polling],
                ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                    "elitescada.driver.test.lifecycle-probe",
                    1,
                    [],
                    []))));
        return components;
    }

    private static EngineeringPackage BuildPackage(bool resourceOwner)
    {
        var tagId = Guid.NewGuid();
        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "Counter",
                "Plant.Lifecycle.Counter",
                TagDataType.Int32,
                Source: SourceKey,
                ReadOnly: true)],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(null, SourceKey, "Lifecycle Probe", DriverType,
                Settings: new Dictionary<string, string>
                {
                    ["resourceOwner"] = resourceOwner.ToString()
                })]);
    }

    private sealed record LifecycleProbePlan(
        string DataSourceKey,
        string Name,
        IReadOnlyCollection<TagDefinition> Tags,
        bool ResourceOwner) : ICommunicationDriverRuntimePlan
    {
        public string DriverType => EngineeringRuntimeLifecycleFailureTests.DriverType;
    }

    private sealed class LifecycleProbePlanner : ICommunicationDriverRuntimePlanner
    {
        public string DriverType => EngineeringRuntimeLifecycleFailureTests.DriverType;

        public CommunicationDriverRuntimePlanningResult Plan(
            EngineeringPackage package,
            DataSourceEngineeringDto dataSource)
        {
            var tags = package.Tags
                .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
                .Select(tag => new TagDefinition(
                    tag.Id ?? Guid.NewGuid(),
                    tag.Name,
                    tag.Path,
                    tag.DataType,
                    tag.Source,
                    tag.EngineeringUnit,
                    tag.Description,
                    tag.ReadOnly,
                    tag.Metadata))
                .ToArray();
            var resourceOwner = dataSource.Settings is not null &&
                dataSource.Settings.TryGetValue("resourceOwner", out var configured) &&
                bool.TryParse(configured, out var parsed) && parsed;

            return new CommunicationDriverRuntimePlanningResult(
                new LifecycleProbePlan(dataSource.Key, dataSource.Name, tags, resourceOwner),
                Array.Empty<EngineeringDriverIssue>());
        }
    }

    private sealed class LifecycleProbeFactory(LifecycleProbeState state) : ICommunicationDriverRuntimeFactory
    {
        public string DriverType => EngineeringRuntimeLifecycleFailureTests.DriverType;

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services)
        {
            var typedPlan = (LifecycleProbePlan)plan;
            var driver = new LifecycleProbeDriver(
                typedPlan.DataSourceKey,
                typedPlan.Name,
                typedPlan.Tags,
                services.Cache,
                services.Registry,
                typedPlan.ResourceOwner,
                state);
            state.Drivers.Add(driver);
            return driver;
        }
    }

    private sealed class LifecycleProbeState
    {
        public List<LifecycleProbeDriver> Drivers { get; } = [];
        public int ThrowOnNextStop;
        public int ThrowOnNextStart;
    }

    private sealed class LifecycleProbeDriver(
        string dataSourceKey,
        string name,
        IReadOnlyCollection<TagDefinition> tags,
        ICurrentTagCache cache,
        ITagRegistry registry,
        bool resourceOwner,
        LifecycleProbeState state) : ICommunicationDriver, ICommunicationDriverResourceClaimSource
    {
        private DriverState _driverState = DriverState.Stopped;

        public string DriverId => $"{EngineeringRuntimeLifecycleFailureTests.DriverType}:{dataSourceKey}";
        public string Name { get; } = name;
        public DriverCapabilities Capabilities => DriverCapabilities.Read;
        public DriverStatus Status => new(DriverId, Name, _driverState, DateTimeOffset.UtcNow);
        public IReadOnlyCollection<TagDefinition> Tags { get; } = tags;
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool IsRunning => _driverState == DriverState.Running;
        public IReadOnlyCollection<CommunicationDriverResourceClaim> ResourceClaims { get; } = resourceOwner
            ? [new CommunicationDriverResourceClaim(
                "test.lifecycle-probe",
                "host",
                "shared-resource",
                dataSourceKey,
                Exclusive: true)]
            : [];

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;
            if (Interlocked.Exchange(ref state.ThrowOnNextStart, 0) == 1)
                throw new InvalidOperationException("Synthetic failure restoring the previous driver.");
            _driverState = DriverState.Running;
            foreach (var tag in Tags)
            {
                if (!registry.TryGet(tag.Id, out _)) registry.Register(tag);
                await cache.UpdateAsync(
                    tag,
                    new TagValue(tag.Id, 7, DateTimeOffset.UtcNow, TagQuality.Good, DriverId),
                    cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCalls++;
            _driverState = DriverState.Stopped;
            if (Interlocked.Exchange(ref state.ThrowOnNextStop, 0) == 1)
                throw new InvalidOperationException("Synthetic failure after the driver stopped its acquisition loop.");
            return Task.CompletedTask;
        }

        public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TagValue?>(null);

        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
