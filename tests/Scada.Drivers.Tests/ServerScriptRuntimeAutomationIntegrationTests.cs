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
using Scada.Historian.Memory;

namespace Scada.Drivers.Tests;

public sealed class ServerScriptRuntimeAutomationIntegrationTests
{
    [Fact]
    public async Task ActiveServerScript_InitializeAndTimer_DriveSharedRuntimeHistorianAndAlarm()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        var package = Package(tagId, initialValue: 0, revisionMarker: "r1");
        await using var historian = new BufferedInMemoryHistorian(eventBus);
        await using var runtime = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2),
            new InMemoryServerMemoryRetentionStore());

        var activated = await runtime.ActivateAsync("server-script-e2e", 1, package);
        Assert.True(activated.Activated);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServerScripts:HandlerTimeoutMs"] = "2000",
                ["ServerScripts:MinimumTimerIntervalMs"] = "10",
                ["ServerScripts:MaxQueuedEvents"] = "16"
            })
            .Build();
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, configuration);

        await manager.ActivateAsync("server-script-e2e", 1, package.Scripts);
        await WaitUntilAsync(() =>
            runtime.TryGetCurrent(tagId, out var current) && Convert.ToInt32(current!.Value) >= 2,
            TimeSpan.FromSeconds(8));
        await WaitUntilAsync(() => historian.WrittenSamples >= 2, TimeSpan.FromSeconds(2));

        Assert.True(runtime.TryGetCurrent(tagId, out var final));
        Assert.True(Convert.ToInt32(final!.Value) >= 2);
        Assert.Contains(historian.Query(tagId, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1)),
            sample => Convert.ToInt32(sample.Value) >= 2);
        Assert.Contains(runtime.Alarms(activeOnly: true), alarm =>
            alarm.TagId == tagId && alarm.Type == AlarmType.High && alarm.State == AlarmState.Active);

        var diagnostics = manager.Snapshot();
        Assert.Equal("server-script-e2e", diagnostics.ProjectKey);
        Assert.Equal(1, diagnostics.Revision);
        Assert.Single(diagnostics.Scripts);

        await manager.DisposeAsync();
        var stoppedValue = Convert.ToInt32(final.Value);
        await Task.Delay(300);
        Assert.True(runtime.TryGetCurrent(tagId, out var afterStop));
        Assert.Equal(stoppedValue, Convert.ToInt32(afterStop!.Value));
    }

    [Fact]
    public async Task RuntimeExecutionCoordinator_IsolatesTimeoutAndFailure()
    {
        var script = new Scada.Engineering.VisualScripting.PythonScriptDefinition(
            Guid.NewGuid(),
            "Scripts.Timeout",
            "Timeout",
            Scada.Engineering.VisualScripting.PythonScriptScope.Server,
            "def timer(event):\n    pass",
            entryPoints: new[]
            {
                new Scada.Engineering.VisualScripting.PythonScriptEntryPoint(
                    Scada.Engineering.VisualScripting.PythonScriptEventKind.Timer,
                    "timer",
                    TimerIntervalMs: 50)
            });
        var executor = new BlockingExecutor();
        await using var coordinator = new Scada.Engineering.VisualScripting.ScriptRuntimeExecutionCoordinator(
            script,
            "test-runtime",
            new Scada.Engineering.VisualScripting.ScriptExecutionPolicy(
                TimeSpan.FromMilliseconds(50), 4, TimeSpan.FromMilliseconds(10), 2),
            executor);

        coordinator.Enqueue(new Scada.Engineering.VisualScripting.ScriptEventIdentity(
            Scada.Engineering.VisualScripting.PythonScriptEventKind.Timer, "timer"));
        var result = await coordinator.ProcessNextAsync();

        Assert.Equal(Scada.Engineering.VisualScripting.ScriptExecutionStatus.TimedOut, result.Execution!.Status);
    }

    private static EngineeringPackage Package(Guid tagId, int initialValue, string revisionMarker)
    {
        var tagReference = tagId.ToString("D");
        var source = $"""
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
                new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "initialize"),
                new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Timer, "timer", TimerIntervalMs: 100)
            },
            dependencies: new[]
            {
                new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ServerMemoryTag, tagReference)
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "ProcessState",
                    "Simulation.ProcessState",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    Historian: new HistorianSettingsDto(true, "on-change"),
                    InitialValue: new MemoryInitialValueDto(
                        TagDataType.Int32,
                        JsonSerializer.SerializeToElement(initialValue)))
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
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            },
            Scripts: new[] { script });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition());
    }

    private sealed class BlockingExecutor : Scada.Engineering.VisualScripting.IPythonScriptHandlerExecutor
    {
        public async ValueTask ExecuteAsync(
            Scada.Engineering.VisualScripting.PythonScriptDefinition script,
            Scada.Engineering.VisualScripting.ScriptEventEnvelope scriptEvent,
            Scada.Engineering.VisualScripting.ScriptExecutionLease lease)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, lease.CancellationToken);
        }
    }
}
