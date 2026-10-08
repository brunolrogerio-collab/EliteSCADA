using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeInternalMemoryTests
{
    [Fact]
    public async Task ActivateAsync_ServerMemoryOnlyPublishesSharedValueAndRoutesWrites()
    {
        var tagId = Guid.NewGuid();
        var alarmId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        var bus = new InMemoryScadaEventBus();
        var observed = 0;
        using var subscription = bus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == tagId) Interlocked.Increment(ref observed);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);

        var result = await runtime.ActivateAsync("memory-project", 1, ServerPackage(tagId, alarmId, "Plant.Counter", 5));

        Assert.True(result.Activated);
        Assert.Empty(runtime.Describe().Drivers);
        Assert.True(runtime.TryGetTag(tagId, out var tag));
        Assert.Equal("Plant.Counter", tag!.Path);
        Assert.True(runtime.TryGetCurrent(tagId, out var initial));
        Assert.Equal(5, initial!.Value);

        await runtime.WriteAsync(tagId, JsonSerializer.SerializeToElement(12));

        Assert.True(runtime.TryGetCurrent(tagId, out var updated));
        Assert.Equal(12, updated!.Value);
        Assert.True(Volatile.Read(ref observed) > 0);
        Assert.Contains(runtime.Alarms(activeOnly: true), x => x.DefinitionId == alarmId);
    }

    [Fact]
    public async Task ActivateAsync_ServerMemoryRetainsByStableIdAcrossRuntimeRestartAndPathRename()
    {
        var tagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();

        await using (var first = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention))
        {
            Assert.True((await first.ActivateAsync(
                "memory-project",
                1,
                ServerPackage(tagId, Guid.NewGuid(), "Plant.Counter", 5))).Activated);
            await first.WriteAsync(tagId, 33);
        }

        await using var second = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);

        var result = await second.ActivateAsync(
            "memory-project",
            2,
            ServerPackage(tagId, Guid.NewGuid(), "Plant.CounterRenamed", 1));

        Assert.True(result.Activated);
        Assert.True(second.TryGetTag(tagId, out var renamed));
        Assert.Equal("Plant.CounterRenamed", renamed!.Path);
        Assert.True(second.TryGetCurrent(tagId, out var restored));
        Assert.Equal(33, restored!.Value);
    }

    [Fact]
    public async Task RestoreAuthoritativeValuesAsync_RestoresAndRetainsPromotedServerMemoryValue()
    {
        var tagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);

        var activation = await runtime.ActivateAsync(
            "memory-project",
            1,
            ServerPackage(tagId, Guid.NewGuid(), "Plant.Counter", 5));
        Assert.True(activation.Activated);
        Assert.True(runtime.TryGetCurrent(tagId, out var initial));
        Assert.Equal(5, initial!.Value);

        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-5);
        var restored = await runtime.RestoreAuthoritativeValuesAsync(new[]
        {
            new TagValue(
                tagId,
                JsonSerializer.SerializeToElement(33),
                timestamp,
                TagQuality.Good,
                "node-a")
        });

        Assert.Equal(1, restored);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(33, current!.Value);

        await using var restarted = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);
        Assert.True((await restarted.ActivateAsync(
            "memory-project",
            2,
            ServerPackage(tagId, Guid.NewGuid(), "Plant.Counter", 1))).Activated);
        Assert.True(restarted.TryGetCurrent(tagId, out var afterRestart));
        Assert.Equal(33, afterRestart!.Value);
    }

    [Fact]
    public async Task MaterializePassiveAsync_ProjectsExactRuntimeWithoutStartingServerMemoryOrAlarmEffects()
    {
        var tagId = Guid.NewGuid();
        var alarmId = Guid.NewGuid();
        var activatedAt = DateTimeOffset.Parse("2026-09-30T18:30:00Z");
        var bus = new InMemoryScadaEventBus();
        var observedTagEvents = 0;
        using var subscription = bus.Subscribe<TagValueChanged>(_ =>
        {
            Interlocked.Increment(ref observedTagEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            new InMemoryServerMemoryRetentionStore());

        var package = ServerPackage(
            tagId,
            alarmId,
            "Plant.PassiveCounter",
            5);
        var result = await runtime.MaterializePassiveAsync(
            "memory-project",
            7,
            package,
            activatedAt);

        Assert.True(result.Activated);
        var descriptor = runtime.Describe();
        Assert.Equal("memory-project", descriptor.ProjectKey);
        Assert.Equal(7, descriptor.Revision);
        Assert.Equal(activatedAt, descriptor.ActivatedAtUtc);
        var captured = Assert.IsType<EngineeringPackage>(runtime.CaptureApplication());
        Assert.Equal(package.Schema, captured.Schema);
        Assert.Equal(package.SchemaVersion, captured.SchemaVersion);
        Assert.Equal(package.ExportedAt, captured.ExportedAt);
        Assert.Equal(package.Tags, captured.Tags);
        Assert.Equal(package.Alarms, captured.Alarms);
        Assert.Equal(package.DataSources, captured.DataSources);

        var projected = Assert.Single(runtime.Tags());
        Assert.Equal(tagId, projected.Id);
        Assert.False(runtime.TryGetCurrent(tagId, out _));
        Assert.Empty(runtime.CurrentValues());
        Assert.Contains(runtime.AlarmDefinitions(), alarm => alarm.Id == alarmId);
        Assert.Empty(runtime.Alarms(activeOnly: true));
        Assert.Equal(0, Volatile.Read(ref observedTagEvents));
    }

    [Fact]
    public async Task MaterializePassiveAsync_KeepsAnyCommunicationDriverStoppedAndProjectsPeerValues()
    {
        const string driverType = "test.passive-probe";
        const string sourceKey = "probe.source";
        var tagId = Guid.NewGuid();
        var driverState = new PassiveProbeDriverState();
        var components = new CommunicationDriverRuntimeComponentRegistry();
        var descriptor = new CommunicationDriverTypeDescriptor(
            driverType,
            "Passive Probe",
            DriverContractVersion: 1,
            RuntimeCapabilities: DriverCapabilities.Read,
            EngineeringCapabilities: DriverEngineeringCapabilities.None,
            AcquisitionModes: [DriverAcquisitionMode.Polling],
            ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                "elitescada.driver.test.passive-probe",
                1,
                [],
                []));
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new PassiveProbeRuntimePlanner(driverType),
            new PassiveProbeRuntimeFactory(driverType, driverState),
            descriptor));

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "Counter",
                "Plant.Probe.Counter",
                TagDataType.Int32,
                Source: sourceKey,
                ReadOnly: true)],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(null, sourceKey, "Probe", driverType)]);

        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(1),
            communicationComponents: components);

        var result = await runtime.MaterializePassiveAsync(
            "passive-probe-project",
            7,
            package,
            DateTimeOffset.UtcNow);

        Assert.True(result.Activated, string.Join(" | ", result.RuntimeIssues.Select(issue => issue.Message)));
        Assert.Equal(0, Volatile.Read(ref driverState.StartCalls));
        Assert.Equal(DriverState.Stopped, Assert.Single(runtime.Describe().Drivers).State);
        Assert.True(runtime.TryGetTag(tagId, out var projectedTag));
        Assert.Equal("Plant.Probe.Counter", projectedTag!.Path);

        var applied = await runtime.ApplyPassiveAuthoritativeValuesAsync(
        [
            new TagValue(tagId, 42, DateTimeOffset.UtcNow, TagQuality.Good, "node-a")
        ]);

        Assert.Equal(1, applied);
        Assert.Equal(0, Volatile.Read(ref driverState.StartCalls));
        Assert.True(runtime.TryGetCurrent(tagId, out var projectedValue));
        Assert.Equal(42, projectedValue!.Value);
        Assert.Equal("node-a", projectedValue.Source);

        var promoted = await runtime.ActivateAsync("passive-probe-project", 8, package);

        Assert.True(promoted.Activated, string.Join(" | ", promoted.RuntimeIssues.Select(issue => issue.Message)));
        Assert.Equal(1, Volatile.Read(ref driverState.StartCalls));
        Assert.Equal(DriverState.Running, Assert.Single(runtime.Describe().Drivers).State);
    }

    [Fact]
    public async Task ApplyPassiveAuthoritativeValuesAsync_HydratesOnlyPassiveCacheWithoutRetainingOrForwarding()
    {
        var tagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        var bus = new InMemoryScadaEventBus();
        var observedTagEvents = 0;
        using var subscription = bus.Subscribe<TagValueChanged>(_ =>
        {
            Interlocked.Increment(ref observedTagEvents);
            return ValueTask.CompletedTask;
        });
        await using var standby = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);

        Assert.True((await standby.MaterializePassiveAsync(
            "memory-project",
            7,
            ServerPackage(tagId, Guid.NewGuid(), "Plant.PassiveCounter", 5),
            DateTimeOffset.UtcNow.AddMinutes(-1))).Activated);

        var timestamp = DateTimeOffset.UtcNow;
        var applied = await standby.ApplyPassiveAuthoritativeValuesAsync(new[]
        {
            new TagValue(
                tagId,
                JsonSerializer.SerializeToElement(42),
                timestamp,
                TagQuality.Good,
                "node-a")
        });

        Assert.Equal(1, applied);
        Assert.True(standby.TryGetCurrent(tagId, out var current));
        Assert.Equal(42, current!.Value);
        Assert.Equal(timestamp, current.Timestamp);
        Assert.Equal("node-a", current.Source);
        Assert.Equal(0, Volatile.Read(ref observedTagEvents));

        await using var active = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);
        Assert.True((await active.ActivateAsync(
            "memory-project",
            8,
            ServerPackage(tagId, Guid.NewGuid(), "Plant.PassiveCounter", 5))).Activated);
        Assert.True(active.TryGetCurrent(tagId, out var activeInitial));
        Assert.Equal(5, activeInitial!.Value);
    }

    [Fact]
    public async Task ActivateAsync_ClientMemoryOnlyDoesNotCreateServerGlobalTagState()
    {
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "SelectedPump",
                    "UI.SelectedPump",
                    TagDataType.String,
                    Source: "memory.client",
                    ReadOnly: false,
                    InitialValue: Initial(TagDataType.String, "P01"))
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.client",
                    "Client Memory",
                    InternalMemoryRuntimePlanner.ClientMemoryDriverKey)
            });

        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1));

        var result = await runtime.ActivateAsync("client-memory-project", 1, package);

        Assert.True(result.Activated);
        Assert.Empty(runtime.Tags());
        Assert.Empty(runtime.CurrentValues());
        Assert.False(runtime.TryGetTag(tagId, out _));
    }

    private static EngineeringPackage ServerPackage(
        Guid tagId,
        Guid alarmId,
        string path,
        int initialValue)
    {
        var tag = new TagEngineeringDto(
            tagId,
            "Counter",
            path,
            TagDataType.Int32,
            Source: "memory.server",
            ReadOnly: false,
            InitialValue: Initial(TagDataType.Int32, initialValue));

        var alarm = new AlarmEngineeringDto(
            alarmId,
            "High counter",
            tagId,
            path,
            AlarmType.High,
            AlarmPriority.High,
            Setpoint: 10,
            Area: "Plant",
            Message: "Counter above 10");

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            new[] { alarm },
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });
    }

    private static MemoryInitialValueDto Initial(TagDataType type, object value) =>
        new(type, JsonSerializer.SerializeToElement(value, value.GetType()));

    private sealed class PassiveProbeRuntimePlan(
        string dataSourceKey,
        string name,
        IReadOnlyCollection<TagDefinition> tags,
        string driverType) : ICommunicationDriverRuntimePlan
    {
        public string DataSourceKey { get; } = dataSourceKey;
        public string Name { get; } = name;
        public IReadOnlyCollection<TagDefinition> Tags { get; } = tags;
        public string DriverType { get; } = driverType;
    }

    private sealed class PassiveProbeRuntimePlanner(string driverType) : ICommunicationDriverRuntimePlanner
    {
        public string DriverType { get; } = driverType;

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

            return new CommunicationDriverRuntimePlanningResult(
                new PassiveProbeRuntimePlan(dataSource.Key, dataSource.Name, tags, DriverType),
                Array.Empty<EngineeringDriverIssue>());
        }
    }

    private sealed class PassiveProbeRuntimeFactory(
        string driverType,
        PassiveProbeDriverState state) : ICommunicationDriverRuntimeFactory
    {
        public string DriverType { get; } = driverType;

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services) =>
            new PassiveProbeDriver(plan.DataSourceKey, plan.Name, plan.Tags, services.Cache, state);
    }

    private sealed class PassiveProbeDriverState
    {
        public int StartCalls;
    }

    private sealed class PassiveProbeDriver(
        string dataSourceKey,
        string name,
        IReadOnlyCollection<TagDefinition> tags,
        ICurrentTagCache cache,
        PassiveProbeDriverState state) : ICommunicationDriver
    {
        private DriverState _state = DriverState.Stopped;

        public string DriverId => $"test.passive-probe:{dataSourceKey}";
        public string Name { get; } = name;
        public DriverCapabilities Capabilities => DriverCapabilities.Read;
        public DriverStatus Status => new(DriverId, Name, _state, DateTimeOffset.UtcNow);
        public IReadOnlyCollection<TagDefinition> Tags { get; } = tags;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref state.StartCalls);
            _state = DriverState.Running;
            foreach (var tag in Tags)
            {
                await cache.UpdateAsync(
                    tag,
                    new TagValue(tag.Id, 99, DateTimeOffset.UtcNow, TagQuality.Good, DriverId),
                    cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = DriverState.Stopped;
            return Task.CompletedTask;
        }

        public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TagValue?>(null);

        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
