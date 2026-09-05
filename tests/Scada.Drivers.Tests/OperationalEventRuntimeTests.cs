using Scada.Api.Historian;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class OperationalEventRuntimeTests
{
    [Fact]
    public async Task RuntimeRejectsOperationalEventEmissionBeforeActivation()
    {
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await runtime.EmitOperationalEventAsync(Guid.NewGuid());
        });
    }

    [Fact]
    public async Task ActiveRevision_EmitsOnlyItsOperationalEventDefinitions()
    {
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var definitionId = Guid.Parse("22000000-0000-0000-0000-000000000002");
        var package = CreatePackage(definitionId, "unit.mode.changed");

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

    [Fact]
    public async Task NewActiveRevision_ReplacesOperationalEventDefinitionAuthority()
    {
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);
        var oldDefinitionId = Guid.Parse("23000000-0000-0000-0000-000000000001");
        var newDefinitionId = Guid.Parse("23000000-0000-0000-0000-000000000002");

        var first = await runtime.ActivateAsync(
            "event-revision-test",
            1,
            CreatePackage(oldDefinitionId, "unit.started"));
        Assert.True(first.Activated, string.Join("; ", first.RuntimeIssues.Select(issue => issue.Message)));
        _ = await runtime.EmitOperationalEventAsync(oldDefinitionId);

        var second = await runtime.ActivateAsync(
            "event-revision-test",
            2,
            CreatePackage(newDefinitionId, "unit.stopped"));
        Assert.True(second.Activated, string.Join("; ", second.RuntimeIssues.Select(issue => issue.Message)));

        Assert.False(runtime.TryGetOperationalEvent(oldDefinitionId, out _));
        Assert.True(runtime.TryGetOperationalEvent(newDefinitionId, out _));
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            _ = await runtime.EmitOperationalEventAsync(oldDefinitionId);
        });

        var emitted = await runtime.EmitOperationalEventAsync(newDefinitionId);
        Assert.Equal(newDefinitionId, emitted.DefinitionId);
    }

    [Fact]
    public void HistoricalQuery_ProtectsOperationalEventsWithRuntimeViewCapability()
    {
        Assert.Equal(
            SecurityCapability.View,
            HistoricalQueryApi.RequiredCapability(HistoricalDatasets.OperationalEvents));
        Assert.NotEqual(
            HistoricalQueryApi.RequiredCapability(HistoricalDatasets.HistorianSamples),
            HistoricalQueryApi.RequiredCapability(HistoricalDatasets.OperationalEvents));
    }

    private static GatewayEngineeringRuntimeCoordinator CreateRuntime(IScadaEventBus bus) =>
        new(
            new EngineeringRuntimeCoordinator(
                bus,
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(1)),
            bus);

    private static EngineeringPackage CreatePackage(Guid definitionId, string definitionKey)
    {
        var tagId = Guid.Parse("22000000-0000-0000-0000-000000000001");
        return new EngineeringPackage(
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
                    definitionKey,
                    definitionKey == "unit.stopped" ? "Unit stopped" : "Unit state changed",
                    "state-change",
                    "operation",
                    "runtime.logic",
                    Area: "Process",
                    EquipmentPath: "Process/Unit01",
                    TagId: tagId,
                    TagPath: "Process/Unit01/Mode",
                    Message: "Unit state changed")
            });
    }
}