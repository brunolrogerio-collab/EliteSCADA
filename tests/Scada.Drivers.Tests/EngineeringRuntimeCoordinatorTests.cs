using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeCoordinatorTests
{
    private const string StagedReadinessDriverType = "test.staged-readiness";

    [Fact]
    public async Task ActivateAsync_VisualOnlyProjectCommitsWithoutAcquisitionSources()
    {
        var screenId = Guid.NewGuid();
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], [], [],
            Screens: [new(screenId, "static-home", "Static Home")], StartupScreenId: screenId);
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(), new EngineeringDriverCompiler(), TimeSpan.FromSeconds(1));
        var committed = false;
        var result = await runtime.ActivateAsync("visual-only", 19, package, (_, _) =>
        {
            committed = true;
            return Task.CompletedTask;
        });
        Assert.True(result.Activated);
        Assert.True(committed);
        Assert.Equal(19, runtime.Describe().Revision);
        Assert.Empty(runtime.Describe().Drivers);
        Assert.DoesNotContain(result.RuntimeIssues, issue => issue.IsError);
        Assert.Contains(result.RuntimeIssues, issue => issue.Code == "RUNTIME_NO_ACTIVE_SOURCES" && !issue.IsError);
    }

    [Fact]
    public async Task ActivateAsync_CandidateCanAcquireInputsWhileWritesAndExternalEventsRemainFencedUntilCommit()
    {
        var tag = TagDefinition.Create(
            "Setpoint",
            "Plant.Setpoint",
            TagDataType.Int16,
            source: "staged-source");
        var components = CreateStagedReadinessComponents(tag);
        var factory = Assert.IsType<StagedReadinessRuntimeFactory>(
            components.GetRequired(StagedReadinessDriverType).Factory);
        var externalBus = new InMemoryScadaEventBus();
        var forwardedTagEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(_ =>
        {
            Interlocked.Increment(ref forwardedTagEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(1),
            communicationComponents: components);

        bool acquiredAtCommit = false;
        bool writesAllowedAtCommit = true;
        int eventsForwardedAtCommit = -1;
        Exception? fencedWriteFailure = null;
        var result = await runtime.ActivateAsync(
            "plant-a",
            1,
            CreateStagedReadinessPackage(tag),
            async (_, cancellationToken) =>
            {
                var candidate = Assert.IsType<StagedReadinessDriver>(factory.Created);
                acquiredAtCommit = candidate.AcquiredInput;
                writesAllowedAtCommit = candidate.Services.CanOwnExternalEffects;
                eventsForwardedAtCommit = Volatile.Read(ref forwardedTagEvents);
                fencedWriteFailure = await Record.ExceptionAsync(async () =>
                    await candidate.WriteAsync(tag.Id, (short)77, cancellationToken));
            });

        Assert.True(result.Activated);
        Assert.True(acquiredAtCommit);
        Assert.False(writesAllowedAtCommit);
        Assert.IsType<InvalidOperationException>(fencedWriteFailure);
        Assert.Equal(0, eventsForwardedAtCommit);
        Assert.Equal(0, Volatile.Read(ref forwardedTagEvents));

        await runtime.WriteAsync(tag.Id, (short)77);

        Assert.Equal(1, Assert.IsType<StagedReadinessDriver>(factory.Created).Writes);
        Assert.True(Volatile.Read(ref forwardedTagEvents) > 0);
    }

    [Fact]
    public async Task ActivateAsync_StandbyAuthorityDoesNotGrantCandidateInputAcquisition()
    {
        var tag = TagDefinition.Create(
            "Setpoint",
            "Plant.Setpoint",
            TagDataType.Int16,
            source: "staged-source");
        var components = CreateStagedReadinessComponents(tag);
        var factory = Assert.IsType<StagedReadinessRuntimeFactory>(
            components.GetRequired(StagedReadinessDriverType).Factory);
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromMilliseconds(100),
            communicationComponents: components,
            industrialEffectAuthority: static () => false);

        var result = await runtime.ActivateAsync(
            "plant-a",
            1,
            CreateStagedReadinessPackage(tag));

        Assert.False(result.Activated);
        Assert.Contains(result.RuntimeIssues, issue => issue.Code == "RUNTIME_CANDIDATE_NOT_READY");
        var candidate = Assert.IsType<StagedReadinessDriver>(factory.Created);
        Assert.Equal(1, candidate.StartCalls);
        Assert.False(candidate.AcquiredInput);
        Assert.False(candidate.Services.CanAcquireInputs);
        Assert.False(candidate.Services.CanOwnExternalEffects);
    }

    [Fact]
    public async Task ActivateAsync_CommitsReadyModbusRuntimeAndRoutesWritesAndAlarms()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 123;
        server.Start();

        var tagId = Guid.NewGuid();
        var alarmId = Guid.NewGuid();
        var package = CreatePackage(server.Port, tagId, alarmId, "holding:10", readOnly: false);
        var externalBus = new InMemoryScadaEventBus();
        var forwardedTagEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == tagId) Interlocked.Increment(ref forwardedTagEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        var result = await runtime.ActivateAsync("plant-a", 1, package);

        Assert.True(result.Activated);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(tagId, out var activeTag));
        Assert.Equal("Plant.Setpoint", activeTag!.Path);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(123d, Convert.ToDouble(current!.Value));
        await WaitForAsync(
            () => runtime.Alarms(activeOnly: true).Any(x => x.DefinitionId == alarmId),
            TimeSpan.FromSeconds(2));

        await runtime.WriteAsync(tagId, (short)77);
        Assert.Equal((ushort)77, server.HoldingRegisters[10]);

        await WaitForAsync(() => Volatile.Read(ref forwardedTagEvents) > 0, TimeSpan.FromSeconds(2));
        await WaitForAsync(
            () => runtime.Alarms().Any(x =>
                x.DefinitionId == alarmId && x.State == AlarmState.Returned),
            TimeSpan.FromSeconds(2));
        Assert.Contains(runtime.Alarms(activeOnly: true), x => x.DefinitionId == alarmId);
        Assert.True(await runtime.AcknowledgeAlarmAsync(alarmId, "operator"));
        Assert.DoesNotContain(runtime.Alarms(activeOnly: true), x => x.DefinitionId == alarmId);

        Assert.Contains(runtime.Describe().Drivers, x => x.DriverId == "modbus.tcp:plc-a");
        Assert.Contains(runtime.AlarmDefinitions(), x => x.Id == alarmId);
    }

    [Fact]
    public async Task ActivateAsync_DriverPollingOutlivesActivationRequestCancellation()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 123;
        server.Start();

        var tagId = Guid.NewGuid();
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        using var activationCancellation = new CancellationTokenSource();

        var result = await runtime.ActivateAsync(
            "plant-a",
            1,
            CreatePackage(server.Port, tagId, Guid.NewGuid(), "holding:10"),
            activationCancellation.Token);

        Assert.True(result.Activated);
        activationCancellation.Cancel();
        server.HoldingRegisters[10] = 234;

        await WaitForAsync(
            () => runtime.TryGetCurrent(tagId, out var current) &&
                  current?.Quality == TagQuality.Good &&
                  Convert.ToDouble(current.Value) == 234d,
            TimeSpan.FromSeconds(3));

        var diagnostics = Assert.Single(runtime.Describe().CommunicationDrivers);
        Assert.True(diagnostics.Counters.Cycles >= 2);
    }

    [Fact]
    public async Task ActivateAsync_FailedCandidateKeepsPreviousRuntimeAndDoesNotLeakCandidateEvents()
    {
        await using var healthyServer = new TestModbusTcpServer();
        healthyServer.HoldingRegisters[10] = 111;
        healthyServer.Start();

        var activeTagId = Guid.NewGuid();
        var activeAlarmId = Guid.NewGuid();
        var healthyPackage = CreatePackage(healthyServer.Port, activeTagId, activeAlarmId, "holding:10");

        var externalBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        var first = await runtime.ActivateAsync("plant-a", 1, healthyPackage);
        Assert.True(first.Activated);
        Assert.Equal(1, runtime.Describe().Revision);

        await using var unavailableServer = new TestModbusTcpServer();
        unavailableServer.Start();
        var unavailablePort = unavailableServer.Port;
        await unavailableServer.StopAsync();

        var candidateTagId = Guid.NewGuid();
        var candidateAlarmId = Guid.NewGuid();
        var candidatePackage = CreatePackage(unavailablePort, candidateTagId, candidateAlarmId, "holding:20");
        var leakedCandidateEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == candidateTagId) Interlocked.Increment(ref leakedCandidateEvents);
            return ValueTask.CompletedTask;
        });

        var second = await runtime.ActivateAsync("plant-a", 2, candidatePackage);

        Assert.False(second.Activated);
        Assert.Contains(second.RuntimeIssues, x => x.Code == "RUNTIME_CANDIDATE_NOT_READY" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(candidateTagId, out _));
        Assert.True(runtime.TryGetCurrent(activeTagId, out var activeValue));
        Assert.Equal(111d, Convert.ToDouble(activeValue!.Value));
        Assert.Equal(0, Volatile.Read(ref leakedCandidateEvents));
    }

    [Fact]
    public async Task ActivateForHaTakeoverAsync_CommitsDegradedModbusRuntimeWhenDeviceIsUnavailable()
    {
        await using var unavailableServer = new TestModbusTcpServer();
        unavailableServer.Start();
        var unavailablePort = unavailableServer.Port;
        await unavailableServer.StopAsync();

        var tagId = Guid.NewGuid();
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromMilliseconds(300));

        var result = await runtime.ActivateForHaTakeoverAsync(
            "plant-a",
            1,
            CreatePackage(unavailablePort, tagId, Guid.NewGuid(), "holding:10"),
            static (_, _) => Task.CompletedTask);

        Assert.True(result.Activated);
        Assert.Contains(result.RuntimeIssues, issue =>
            issue.Code == "RUNTIME_ACTIVATED_DEGRADED" && !issue.IsError);
        Assert.DoesNotContain(result.RuntimeIssues, issue => issue.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.Contains(runtime.Describe().Drivers, driver => driver.State == Scada.Drivers.Abstractions.DriverState.Running);
        await WaitForAsync(
            () => runtime.TryGetCurrent(tagId, out var current) &&
                  current?.Quality == TagQuality.BadCommunication,
            TimeSpan.FromSeconds(2));

        await WaitForAsync(
            () => Assert.Single(runtime.Describe().CommunicationDrivers).Counters.Cycles >= 2,
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ActivateAsync_CommitFailureKeepsPreviousRuntimeAndCandidateEventsGated()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 90;
        activeServer.Start();

        await using var candidateServer = new TestModbusTcpServer();
        candidateServer.HoldingRegisters[20] = 140;
        candidateServer.Start();

        var activeTagId = Guid.NewGuid();
        var candidateTagId = Guid.NewGuid();
        var externalBus = new InMemoryScadaEventBus();
        var leakedCandidateEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == candidateTagId) Interlocked.Increment(ref leakedCandidateEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        Assert.True((await runtime.ActivateAsync(
            "plant-a",
            1,
            CreatePackage(activeServer.Port, activeTagId, Guid.NewGuid(), "holding:10"))).Activated);

        var commitCalled = false;
        var result = await runtime.ActivateAsync(
            "plant-a",
            2,
            CreatePackage(candidateServer.Port, candidateTagId, Guid.NewGuid(), "holding:20"),
            (_, _) =>
            {
                commitCalled = true;
                throw new InvalidOperationException("Persistence rejected activation.");
            });

        Assert.True(commitCalled);
        Assert.False(result.Activated);
        Assert.Contains(result.RuntimeIssues, x => x.Code == "RUNTIME_ACTIVATION_COMMIT_FAILED" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(candidateTagId, out _));
        Assert.Equal(0, Volatile.Read(ref leakedCandidateEvents));
    }

    [Fact]
    public async Task ActivateAsync_CompilationFailureDoesNotReplaceActiveRuntime()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 50;
        server.Start();

        var activeTagId = Guid.NewGuid();
        var package = CreatePackage(server.Port, activeTagId, Guid.NewGuid(), "holding:10");
        var externalBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        Assert.True((await runtime.ActivateAsync("plant-a", 1, package)).Activated);

        var invalidTagId = Guid.NewGuid();
        var invalid = CreatePackage(server.Port, invalidTagId, Guid.NewGuid(), "40001");
        var result = await runtime.ActivateAsync("plant-a", 2, invalid);

        Assert.False(result.Activated);
        Assert.Contains(result.CompilationIssues, x => x.Code == "MODBUS_TAG_ADDRESS_INVALID" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(invalidTagId, out _));
    }

    private static EngineeringPackage CreatePackage(
        int port,
        Guid tagId,
        Guid alarmId,
        string address,
        bool readOnly = true)
    {
        var tag = new TagEngineeringDto(
            tagId,
            "Setpoint",
            "Plant.Setpoint",
            TagDataType.Int16,
            Source: "plc-a",
            Address: address,
            ReadOnly: readOnly);

        var alarm = new AlarmEngineeringDto(
            alarmId,
            "High setpoint",
            tagId,
            tag.Path,
            AlarmType.High,
            AlarmPriority.High,
            Setpoint: 100,
            Area: "Plant",
            Message: "Setpoint above 100");

        var dataSource = new DataSourceEngineeringDto(
            null,
            "plc-a",
            "PLC A",
            EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scanIntervalMilliseconds"] = "25",
                ["requestTimeoutMilliseconds"] = "500",
                ["unitId"] = "1"
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            new[] { alarm },
            new[] { dataSource });
    }

    private static CommunicationDriverRuntimeComponentRegistry CreateStagedReadinessComponents(TagDefinition tag)
    {
        var components = new CommunicationDriverRuntimeComponentRegistry();
        var schema = new DriverConfigurationSchemaDescriptor(
            "elitescada.driver.test.staged-readiness",
            1,
            [],
            []);
        var descriptor = new CommunicationDriverTypeDescriptor(
            StagedReadinessDriverType,
            "Staged Readiness Test Driver",
            DriverContractVersion: 1,
            RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write,
            EngineeringCapabilities: DriverEngineeringCapabilities.None,
            AcquisitionModes: [DriverAcquisitionMode.Polling],
            ConfigurationSchema: schema);
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new StagedReadinessRuntimePlanner(tag),
            new StagedReadinessRuntimeFactory(tag),
            descriptor));
        return components;
    }

    private static EngineeringPackage CreateStagedReadinessPackage(TagDefinition tag)
    {
        var tagDto = new TagEngineeringDto(
            tag.Id,
            tag.Name,
            tag.Path,
            tag.DataType,
            Source: "staged-source",
            Address: "input:0",
            ReadOnly: false);
        var source = new DataSourceEngineeringDto(
            null,
            "staged-source",
            "Staged Source",
            StagedReadinessDriverType,
            Settings: new Dictionary<string, string>());
        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [tagDto],
            [],
            [source]);
    }

    private sealed record StagedReadinessRuntimePlan(
        string DataSourceKey,
        string Name,
        IReadOnlyCollection<TagDefinition> Tags) : ICommunicationDriverRuntimePlan
    {
        public string DriverType => StagedReadinessDriverType;
    }

    private sealed class StagedReadinessRuntimePlanner(TagDefinition tag) : ICommunicationDriverRuntimePlanner
    {
        public string DriverType => StagedReadinessDriverType;

        public CommunicationDriverRuntimePlanningResult Plan(
            EngineeringPackage package,
            DataSourceEngineeringDto dataSource) =>
            new(new StagedReadinessRuntimePlan(dataSource.Key, dataSource.Name, [tag]), []);
    }

    private sealed class StagedReadinessRuntimeFactory(TagDefinition tag) : ICommunicationDriverRuntimeFactory
    {
        public string DriverType => StagedReadinessDriverType;
        public StagedReadinessDriver? Created { get; private set; }

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services)
        {
            Created = new StagedReadinessDriver(tag, services);
            return Created;
        }
    }

    private sealed class StagedReadinessDriver(
        TagDefinition tag,
        CommunicationDriverRuntimeServices services) : ICommunicationDriver
    {
        public string DriverId => "test.staged-readiness:staged-source";
        public string Name => "Staged Readiness Test Driver";
        public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write;
        public DriverStatus Status { get; private set; } = new(
            "test.staged-readiness:staged-source",
            "Staged Readiness Test Driver",
            DriverState.Stopped,
            DateTimeOffset.UtcNow);
        public IReadOnlyCollection<TagDefinition> Tags => [tag];
        public CommunicationDriverRuntimeServices Services { get; } = services;
        public bool AcquiredInput { get; private set; }
        public int StartCalls { get; private set; }
        public int Writes { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;
            if (!Services.CanAcquireInputs)
            {
                Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
                return Task.CompletedTask;
            }

            if (!Services.Registry.TryGet(tag.Id, out _))
                Services.Registry.Register(tag);
            AcquiredInput = true;
            Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
            return Services.Cache.UpdateAsync(tag, TagValue.Good(tag.Id, (short)12, DriverId), cancellationToken).AsTask();
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }

        public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Services.Cache.TryGet(tagId, out var current);
            return ValueTask.FromResult(current);
        }

        public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Services.CanOwnExternalEffects)
                throw new InvalidOperationException("Runtime candidate cannot write before its commit.");
            if (tagId != tag.Id)
                throw new KeyNotFoundException($"Test driver does not own TAG '{tagId}'.");

            Writes++;
            await Services.Cache.UpdateAsync(tag, TagValue.Good(tag.Id, value, DriverId), cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(20);
        }

        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }
}
