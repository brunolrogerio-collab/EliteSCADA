using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;
using Scada.Historian.Memory;

namespace Scada.Drivers.Tests;

public sealed class ServerScriptRuntimeAutomationIntegrationTests
{
    [Fact]
    public async Task ActiveServerScript_InitializeAndTimer_DriveSharedRuntimeHistorianAndAlarm()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        var package = TimerPackage(tagId, initialValue: 0, revisionMarker: "r1");
        await using var historian = new BufferedInMemoryHistorian(eventBus);
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        var activated = await manager.ActivateRuntimeAsync("server-script-e2e", 1, package);
        Assert.True(activated.Activated);

        await WaitUntilAsync(() =>
            runtime.TryGetCurrent(tagId, out var current) && Convert.ToInt32(current!.Value) >= 2,
            TimeSpan.FromSeconds(8));
        await WaitUntilAsync(() => historian.WrittenSamples >= 2, TimeSpan.FromSeconds(2));

        Assert.True(runtime.TryGetCurrent(tagId, out var final));
        Assert.True(Convert.ToInt32(final!.Value) >= 2);
        Assert.Contains(
            historian.Query(
                tagId,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(1)),
            sample => Convert.ToInt32(sample.Value) >= 2);
        Assert.Contains(runtime.Alarms(activeOnly: true), alarm =>
            alarm.TagId == tagId &&
            alarm.Type == AlarmType.High &&
            alarm.State == AlarmState.Active);

        var diagnostics = manager.Snapshot();
        Assert.Equal("server-script-e2e", diagnostics.ProjectKey);
        Assert.Equal(1, diagnostics.Revision);
        Assert.Single(diagnostics.Scripts);

        await manager.DisposeAsync();
        Assert.True(runtime.TryGetCurrent(tagId, out var stopped));
        var stoppedValue = Convert.ToInt32(stopped!.Value);
        await Task.Delay(300);
        Assert.True(runtime.TryGetCurrent(tagId, out var afterStop));
        Assert.Equal(stoppedValue, Convert.ToInt32(afterStop!.Value));
    }

    [Fact]
    public async Task RED_3_ServerScript_ReadableTagReference_ResolvesDeclaredStableDependency()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        Assert.True((await manager.ActivateRuntimeAsync(
            "readable-tag-red",
            1,
            TimerPackage(tagId, initialValue: 0, revisionMarker: "readable", readableReference: true))).Activated);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while ((!runtime.TryGetCurrent(tagId, out var current) || Convert.ToInt32(current!.Value) < 1) &&
               DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(
            runtime.TryGetCurrent(tagId, out var final) && Convert.ToInt32(final!.Value) >= 1,
            manager.Snapshot().Scripts.Single().Diagnostics.LastSanitizedError);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task ServerScript_CaseVariantReadableTagReference_ResolvesTheDeclaredStableDependency()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        Assert.True((await manager.ActivateRuntimeAsync(
            "case-readable-tag",
            1,
            TimerPackage(tagId, initialValue: 0, revisionMarker: "case-readable", readableReference: true, caseVariantReference: true))).Activated);

        await WaitUntilAsync(
            () => runtime.TryGetCurrent(tagId, out var current) && Convert.ToInt32(current!.Value) >= 1,
            TimeSpan.FromSeconds(3));

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task ServerScript_MultipleReadableTagReferencesResolveOnlyTheirDeclaredStableDependencies()
    {
        var eventBus = new InMemoryScadaEventBus();
        var stateId = Guid.NewGuid();
        var incrementId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        Assert.True((await manager.ActivateRuntimeAsync(
            "multi-readable-tag",
            1,
            MultiReadableTimerPackage(stateId, incrementId))).Activated);

        await WaitUntilAsync(
            () => runtime.TryGetCurrent(stateId, out var state) && Convert.ToInt32(state!.Value) >= 2,
            TimeSpan.FromSeconds(4));

        Assert.True(runtime.TryGetCurrent(incrementId, out var increment));
        Assert.Equal(2, Convert.ToInt32(increment!.Value));
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task RevisionBoundAccess_RejectsObsoleteGenerationWithSameStableTagId()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        Assert.True((await manager.ActivateRuntimeAsync(
            "revision-bound",
            1,
            MemoryPackage(tagId, 1))).Activated);
        Assert.True((await manager.ActivateRuntimeAsync(
            "revision-bound",
            2,
            MemoryPackage(tagId, 2))).Activated);

        await Assert.ThrowsAsync<ScriptExecutionDiagnosticException>(async () =>
            await manager.WriteTagAsync(
                "revision-bound",
                1,
                tagId,
                999,
                serverMemoryOnly: true,
                CancellationToken.None));

        Assert.True(runtime.TryGetCurrent(tagId, out var afterRejectedWrite));
        Assert.NotEqual(999, Convert.ToInt32(afterRejectedWrite!.Value));

        await manager.WriteTagAsync(
            "revision-bound",
            2,
            tagId,
            7,
            serverMemoryOnly: true,
            CancellationToken.None);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(7, Convert.ToInt32(current!.Value));

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task TagChangedAndServerRuntimeEvent_DispatchAgainstSameActiveState()
    {
        var eventBus = new InMemoryScadaEventBus();
        var triggerId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        Assert.True((await manager.ActivateRuntimeAsync(
            "event-runtime",
            1,
            EventPackage(triggerId, stateId))).Activated);

        await runtime.WriteAsync(triggerId, 1);
        await WaitUntilAsync(() =>
            runtime.TryGetCurrent(stateId, out var state) && Convert.ToInt32(state!.Value) == 1,
            TimeSpan.FromSeconds(5));

        await manager.DispatchRuntimeEventAsync("pulse");
        await WaitUntilAsync(() =>
            runtime.TryGetCurrent(stateId, out var state) && Convert.ToInt32(state!.Value) == 11,
            TimeSpan.FromSeconds(5));

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task RuntimeExecutionCoordinator_EnforcesTimeout()
    {
        var script = RuntimeFoundationScript();
        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "timeout-runtime",
            new ScriptExecutionPolicy(
                TimeSpan.FromMilliseconds(50),
                4,
                TimeSpan.FromMilliseconds(10),
                2),
            new BlockingExecutor());

        coordinator.Enqueue(new ScriptEventIdentity(PythonScriptEventKind.Timer, "timer"));
        var result = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptExecutionStatus.TimedOut, result.Execution!.Status);
    }

    [Fact]
    public async Task RuntimeExecutionCoordinator_ContainsHandlerFailure()
    {
        var script = RuntimeFoundationScript();
        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "failure-runtime",
            new ScriptExecutionPolicy(
                TimeSpan.FromMilliseconds(500),
                4,
                TimeSpan.FromMilliseconds(10),
                2),
            new FaultingExecutor());

        coordinator.Enqueue(new ScriptEventIdentity(PythonScriptEventKind.Timer, "timer"));
        var result = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptExecutionStatus.Faulted, result.Execution!.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Execution.SanitizedError));
    }

    private static EngineeringRuntimeCoordinator CreateRuntime(InMemoryScadaEventBus eventBus) =>
        new(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2),
            new InMemoryServerMemoryRetentionStore());

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServerScripts:HandlerTimeoutMs"] = "2000",
                ["ServerScripts:MinimumTimerIntervalMs"] = "10",
                ["ServerScripts:MaxQueuedEvents"] = "16",
                ["ServerScripts:MaxConsecutiveFailuresBeforeThrottle"] = "5"
            })
            .Build();

    private static EngineeringPackage TimerPackage(
        Guid tagId,
        int initialValue,
        string revisionMarker,
        bool readableReference = false,
        bool caseVariantReference = false)
    {
        var tagReference = tagId.ToString("D");
        var readableTagReference = caseVariantReference
            ? "simulation.processstate"
            : "Simulation.ProcessState";
        var source = readableReference
            ? $"""
def timer(event):
    current = read_tag("{readableTagReference}")
    write_tag("{readableTagReference}", current + 1)
"""
            : $"""
def initialize(event):
    write_server_memory("{tagReference}", 1)

def timer(event):
    current = read_server_memory("{tagReference}")
    write_server_memory("{tagReference}", current + 1)
""";
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            $"Scripts.Process.{revisionMarker}",
            "Generic Stateful Process",
            ScriptEngineeringScope.Server,
            source,
            entryPoints: new[]
            {
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Timer,
                    "timer",
                    TimerIntervalMs: 100)
            }.Concat(readableReference
                ? Array.Empty<ScriptEngineeringEntryPoint>()
                : new[] { new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "initialize") })
             .ToArray(),
            dependencies: new[]
            {
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    tagReference,
                    readableReference
                        ? new ScriptTagReferenceBinding(
                            1,
                            "Simulation.ProcessState",
                            new TagValueReference(tagId))
                        : null)
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                ServerMemoryTag(
                    tagId,
                    "ProcessState",
                    "Simulation.ProcessState",
                    initialValue,
                    historian: true)
            },
            new[]
            {
                new AlarmEngineeringDto(
                    null,
                    "Process state high",
                    tagId,
                    "Simulation.ProcessState",
                    AlarmType.High,
                    AlarmPriority.High,
                    Setpoint: 1.5)
            },
            ServerMemoryDataSource(),
            Scripts: new[] { script });
    }

    private static EngineeringPackage MultiReadableTimerPackage(Guid stateId, Guid incrementId)
    {
        const string statePath = "Simulation.ProcessState";
        const string incrementPath = "Simulation.ProcessIncrement";
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "Scripts.Process.MultiReadable",
            "Multi Readable Process",
            ScriptEngineeringScope.Server,
            $$"""
def timer(event):
    state = read_tag("{{statePath}}")
    increment = read_tag("{{incrementPath}}")
    write_tag("{{statePath}}", state + increment)
""",
            entryPoints: [new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Timer, "timer", TimerIntervalMs: 100)],
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    stateId.ToString("D"),
                    new ScriptTagReferenceBinding(1, statePath, new TagValueReference(stateId))),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    incrementId.ToString("D"),
                    new ScriptTagReferenceBinding(1, incrementPath, new TagValueReference(incrementId)))
            ]);

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                ServerMemoryTag(stateId, "ProcessState", statePath, 0, historian: false),
                ServerMemoryTag(incrementId, "ProcessIncrement", incrementPath, 2, historian: false)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            ServerMemoryDataSource(),
            Scripts: [script]);
    }

    private static EngineeringPackage MemoryPackage(Guid tagId, int initialValue) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                ServerMemoryTag(
                    tagId,
                    "State",
                    "Simulation.State",
                    initialValue,
                    historian: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            ServerMemoryDataSource());

    private static EngineeringPackage EventPackage(Guid triggerId, Guid stateId)
    {
        var triggerReference = triggerId.ToString("D");
        var stateReference = stateId.ToString("D");
        var source = $"""
def changed(event):
    current = read_server_memory("{stateReference}")
    write_server_memory("{stateReference}", current + 1)

def pulse(event):
    current = read_server_memory("{stateReference}")
    write_server_memory("{stateReference}", current + 10)
""";
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "Scripts.Events",
            "Generic Event Process",
            ScriptEngineeringScope.Server,
            source,
            entryPoints: new[]
            {
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.TagChanged,
                    "changed",
                    TagReference: new TagValueReference(triggerId)),
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ServerRuntimeEvent,
                    "pulse",
                    TargetReference: "pulse")
            },
            dependencies: new[]
            {
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    triggerReference),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    stateReference)
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                ServerMemoryTag(triggerId, "Trigger", "Simulation.Trigger", 0, historian: false),
                ServerMemoryTag(stateId, "State", "Simulation.State", 0, historian: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            ServerMemoryDataSource(),
            Scripts: new[] { script });
    }

    private static TagEngineeringDto ServerMemoryTag(
        Guid id,
        string name,
        string path,
        int initialValue,
        bool historian) =>
        new(
            id,
            name,
            path,
            TagDataType.Int32,
            Source: "memory.server",
            ReadOnly: false,
            Historian: historian ? new HistorianSettingsDto(true, "on-change") : null,
            InitialValue: new MemoryInitialValueDto(
                TagDataType.Int32,
                JsonSerializer.SerializeToElement(initialValue)));

    private static IReadOnlyCollection<DataSourceEngineeringDto> ServerMemoryDataSource() =>
        new[]
        {
            new DataSourceEngineeringDto(
                null,
                "memory.server",
                "Server Memory",
                InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
        };

    private static PythonScriptDefinition RuntimeFoundationScript() =>
        new(
            Guid.NewGuid(),
            "Scripts.RuntimeFoundation",
            "Runtime Foundation",
            PythonScriptScope.Server,
            "def timer(event):\n    pass",
            entryPoints: new[]
            {
                new PythonScriptEntryPoint(
                    PythonScriptEventKind.Timer,
                    "timer",
                    TimerIntervalMs: 50)
            });

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition());
    }

    private sealed class BlockingExecutor : IPythonScriptHandlerExecutor
    {
        public async ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, lease.CancellationToken);
        }
    }

    private sealed class FaultingExecutor : IPythonScriptHandlerExecutor
    {
        public ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease) =>
            ValueTask.FromException(new InvalidOperationException("untrusted host details"));
    }
}
