using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class OperationalEventRuntimeTests
{
    [Fact]
    public async Task ActiveRevision_EmitsOnlyItsOperationalEventDefinitions()
    {
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(
                bus,
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(1)),
            bus);

        var tagId = Guid.Parse("22000000-0000-0000-0000-000000000001");
        var definitionId = Guid.Parse("22000000-0000-0000-0000-000000000002");
        var package = new EngineeringPackage(
            "scada.engineering",
            16,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "Client mode",
                    "Process/Unit01/Mode",
                    TagDataType.Int32,
                    Source: "memory.client",
                    ReadOnly: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: new[]
            {
                new DataSourceEngineeringDto(
                    Guid.Parse("22000000-0000-0000-0000-000000000003"),
                    "memory.client",
                    "Client Memory",
                    InternalMemoryRuntimePlanner.ClientMemoryDriverKey)
            },
            OperationalEvents: new[]
            {
                new OperationalEventEngineeringDto(
                    definitionId,
                    "unit.mode.changed",
                    "Unit mode changed",
                    "state-change",
                    "operation",
                    "runtime.logic",
                    Area: "Process",
                    EquipmentPath: "Process/Unit01",
                    Message: "Mode changed")
            });

        var activation = await runtime.ActivateAsync("event-runtime-test", 1, package);
        Assert.True(activation.Activated, string.Join("; ", activation.RuntimeIssues.Select(issue => issue.Message)));
        Assert.True(runtime.TryGetOperationalEvent(definitionId, out var definition));
        Assert.NotNull(definition);

        OperationalEventOccurred? observed = null;
        using var subscription = bus.Subscribe<OperationalEventOccurred>(occurrence =>
        {
            observed = occurrence;
            return ValueTask.CompletedTask;
        });

        var emitted = await runtime.EmitOperationalEventAsync(
            definitionId,
            new OperationalEventEmissionContext(
                Operator: "operator-1",
                Operation: "mode-select",
                Context: new Dictionary<string, string> { ["mode"] = "auto" }));

        Assert.Same(emitted, observed);
        Assert.Equal(definitionId, emitted.DefinitionId);
        Assert.Equal("operator-1", emitted.Operator);
        Assert.Equal("auto", emitted.Context["mode"]);
        Assert.NotEqual(Guid.Empty, emitted.EventId);

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            _ = await runtime.EmitOperationalEventAsync(Guid.NewGuid());
        });
    }
}