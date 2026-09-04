using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;

namespace Scada.Drivers.Tests;

public sealed class ServerScriptOperationalEventBridgeIntegrationTests
{
    [Fact]
    public async Task InitializeScript_EmitsCanonicalOperationalEvent_AndPreservesServerMemoryBehavior()
    {
        var eventBus = new InMemoryScadaEventBus();
        var stateTagId = Guid.Parse("c1900000-0000-0000-0000-000000000001");
        var definitionId = Guid.Parse("c1900000-0000-0000-0000-000000000002");
        OperationalEventOccurred? observed = null;
        using var subscription = eventBus.Subscribe<OperationalEventOccurred>(occurrence =>
        {
            observed = occurrence;
            return ValueTask.CompletedTask;
        });

        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());
        ServerScriptOperationalEventBridge.Bind(manager, runtime);

        var activation = await manager.ActivateRuntimeAsync(
            "c19-script-event",
            1,
            ScriptEventPackage(stateTagId, definitionId));

        Assert.True(
            activation.Activated,
            string.Join("; ", activation.RuntimeIssues.Select(issue => issue.Message)));
        await WaitUntilAsync(() => observed is not null, TimeSpan.FromSeconds(5));

        Assert.True(runtime.TryGetCurrent(stateTagId, out var state));
        Assert.Equal(42, Convert.ToInt32(state!.Value));

        var occurrence = Assert.IsType<OperationalEventOccurred>(observed);
        Assert.Equal(definitionId, occurrence.DefinitionId);
        Assert.Equal("unit.mode.changed", occurrence.DefinitionKey);
        Assert.Equal("state-change", occurrence.Type);
        Assert.Equal("operation", occurrence.Category);
        Assert.Equal("runtime.logic", occurrence.Source);
        Assert.Equal("Script override", occurrence.Message);
        Assert.Equal("yes", occurrence.Context["authored"]);
        Assert.Equal("auto", occurrence.Context["mode"]);

        // Context keys that look like occurrence fields remain inert context. The
        // canonical definition remains authoritative for occurrence identity.
        Assert.Equal("forged-type", occurrence.Context["type"]);
        Assert.Equal("forged-category", occurrence.Context["category"]);
        Assert.Equal("forged-source", occurrence.Context["source"]);
        Assert.Equal("forged-definition", occurrence.Context["definitionId"]);
        Assert.NotEqual(occurrence.Context["type"], occurrence.Type);
        Assert.NotEqual(occurrence.Context["category"], occurrence.Category);
        Assert.NotEqual(occurrence.Context["source"], occurrence.Source);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Bridge_RejectsUnknownDisabledAndStaleRevision_FailClosed()
    {
        var eventBus = new InMemoryScadaEventBus();
        var enabledDefinitionId = Guid.Parse("c1900000-0000-0000-0000-000000000010");
        var disabledDefinitionId = Guid.Parse("c1900000-0000-0000-0000-000000000011");

        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());
        ServerScriptOperationalEventBridge.Bind(manager, runtime);

        var first = await manager.ActivateRuntimeAsync(
            "c19-script-event-negative",
            1,
            EventOnlyPackage(enabledDefinitionId, enabled: true));
        Assert.True(first.Activated);

        await Assert.ThrowsAsync<ScriptExecutionDiagnosticException>(async () =>
            _ = await ServerScriptOperationalEventBridge.EmitAsync(
                manager,
                "c19-script-event-negative",
                1,
                Guid.Parse("c1900000-0000-0000-0000-000000000099"),
                null,
                null));

        var second = await manager.ActivateRuntimeAsync(
            "c19-script-event-negative",
            2,
            EventOnlyPackage(disabledDefinitionId, enabled: false));
        Assert.True(second.Activated);

        await Assert.ThrowsAsync<ScriptExecutionDiagnosticException>(async () =>
            _ = await ServerScriptOperationalEventBridge.EmitAsync(
                manager,
                "c19-script-event-negative",
                2,
                disabledDefinitionId,
                null,
                null));

        await Assert.ThrowsAsync<ScriptExecutionDiagnosticException>(async () =>
            _ = await ServerScriptOperationalEventBridge.EmitAsync(
                manager,
                "c19-script-event-negative",
                1,
                enabledDefinitionId,
                null,
                null));

        await manager.DisposeAsync();
    }

    private static GatewayEngineeringRuntimeCoordinator CreateRuntime(InMemoryScadaEventBus eventBus) =>
        new(
            new EngineeringRuntimeCoordinator(
                eventBus,
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(2),
                new InMemoryServerMemoryRetentionStore()),
            eventBus);

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

    private static EngineeringPackage ScriptEventPackage(Guid stateTagId, Guid definitionId)
    {
        var tagReference = stateTagId.ToString("D");
        var definitionReference = definitionId.ToString("D");
        var source = $$"""
def initialize(event):
    write_server_memory("{{tagReference}}", 42)
    emit_operational_event("{{definitionReference}}", "Script override", {"mode": "auto", "type": "forged-type", "category": "forged-category", "source": "forged-source", "definitionId": "forged-definition"})
""";

        var script = new ScriptEngineeringDefinition(
            Guid.Parse("c1900000-0000-0000-0000-000000000003"),
            "Scripts.OperationalEventBridge",
            "Operational Event bridge",
            ScriptEngineeringScope.Server,
            source,
            entryPoints: new[]
            {
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Initialize,
                    "initialize")
            },
            dependencies: new[]
            {
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    tagReference)
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    stateTagId,
                    "State",
                    "Simulation.State",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: new MemoryInitialValueDto(
                        TagDataType.Int32,
                        JsonSerializer.SerializeToElement(0)))
            },
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            },
            Scripts: new[] { script },
            OperationalEvents: new[]
            {
                OperationalEvent(definitionId, enabled: true)
            });
    }

    private static EngineeringPackage EventOnlyPackage(Guid definitionId, bool enabled) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            OperationalEvents: new[]
            {
                OperationalEvent(definitionId, enabled)
            });

    private static OperationalEventEngineeringDto OperationalEvent(Guid definitionId, bool enabled) =>
        new(
            definitionId,
            "unit.mode.changed",
            "Unit mode changed",
            "state-change",
            "operation",
            "runtime.logic",
            Message: "Authored message",
            Enabled: enabled,
            Metadata: new Dictionary<string, string>
            {
                ["authored"] = "yes"
            });

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition());
    }
}
