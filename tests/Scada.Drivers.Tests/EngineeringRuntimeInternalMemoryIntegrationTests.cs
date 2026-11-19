using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Abstractions;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeInternalMemoryIntegrationTests
{
    [Fact]
    public async Task ActivateAsync_PreservesControlledUnitThroughSerializationAndReactivation()
    {
        var tagId = Guid.NewGuid();
        var unit = new ControlledEngineeringUnitReference(2, "unit.power.watt");
        var package = ServerPackage(tagId, 5);
        package = package with { Tags = package.Tags.Select(tag => tag with
        {
            EngineeringUnit = "W", ControlledEngineeringUnit = unit
        }).ToArray() };
        var persisted = JsonSerializer.Deserialize<EngineeringPackage>(JsonSerializer.Serialize(package))!;
        await using var runtime = CreateRuntime(new InMemoryServerMemoryRetentionStore());

        foreach (var revision in new[] { 1, 2 })
        {
            Assert.True((await runtime.ActivateAsync("memory-units", revision, persisted)).Activated);
            Assert.True(runtime.TryGetTag(tagId, out var active));
            Assert.Equal(unit, active!.ControlledEngineeringUnit);
            Assert.Equal("W", active.EngineeringUnit);
        }
    }

    [Fact]
    public async Task ResetServerMemoryRetainedValueAsync_ClearsRetentionAndPublishesEngineeredInitialValue()
    {
        var tagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        await using var runtime = CreateRuntime(retention);

        Assert.True((await runtime.ActivateAsync("memory-reset", 1, ServerPackage(tagId, 5))).Activated);
        await runtime.WriteAsync(tagId, 33);
        Assert.NotNull(await retention.ReadAsync(tagId));

        await runtime.ResetServerMemoryRetainedValueAsync(tagId);

        Assert.Null(await retention.ReadAsync(tagId));
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(5, current!.Value);
    }

    [Fact]
    public async Task WriteAsync_ServerMemoryRejectsJsonValueWithWrongEngineeredType()
    {
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(new InMemoryServerMemoryRetentionStore());
        Assert.True((await runtime.ActivateAsync("memory-types", 1, ServerPackage(tagId, 5))).Activated);

        var incompatible = JsonSerializer.SerializeToElement("not-an-int32");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await runtime.WriteAsync(tagId, incompatible));

        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(5, current!.Value);
    }

    [Fact]
    public async Task ClientMemorySources_ExposeTypedDefinitionsWithoutRegisteringGlobalServerTag()
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
                    "Counter64",
                    "UI.Counter64",
                    TagDataType.Int64,
                    Source: "memory.client",
                    ReadOnly: false,
                    InitialValue: Initial(TagDataType.Int64, long.MaxValue))
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

        await using var runtime = CreateRuntime(new InMemoryServerMemoryRetentionStore());
        Assert.True((await runtime.ActivateAsync("client-memory-catalog", 1, package)).Activated);

        var source = Assert.Single(runtime.ClientMemorySources());
        var tag = Assert.Single(source.Tags);
        Assert.Equal(tagId, tag.Tag.Id);
        Assert.Equal(TagDataType.Int64, tag.InitialValue.DataType);
        Assert.Equal(long.MaxValue, tag.InitialValue.Value);
        Assert.Empty(runtime.Tags());
        Assert.Empty(runtime.CurrentValues());
    }

    [Fact]
    public async Task IntelligenceDeviationRequiresActivePermitAndOperatingContextAndUsesCanonicalAlarmState()
    {
        var targetId = Guid.NewGuid();
        var conditionId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var package = ServerPackage(targetId, 5);
        var condition = package.Tags.Single() with
        {
            Id = conditionId, Name = "Pump mode", Path = "Plant.Mode", InitialValue = Initial(TagDataType.Int32, 0)
        };
        package = package with
        {
            Tags = [.. package.Tags, condition],
            Equipment = [new(equipmentId, "Plant.Pump01", "Pump 01")],
            AssetProcessIntelligence = new(3,
                Assignments: [new(assignmentId, equipmentId, profileId)],
                OperatingContextProfiles: [new(Guid.NewGuid(), "pumping", "Pumping", assignmentId, 1,
                    [new(conditionId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 1, 2)],
                    new(targetId, 3, 7, 0))])
        };
        var permitted = false;
        var active = true;
        var bus = new InMemoryScadaEventBus();
        var changes = new List<AlarmStateChanged>();
        using var subscription = bus.Subscribe<AlarmStateChanged>(change =>
        {
            changes.Add(change);
            return ValueTask.CompletedTask;
        });
        await using var runtime = new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(),
            industrialEffectAuthority: () => active,
            intelligenceAlarmPermit: (project, revision, equipment) =>
                permitted && project == "eee" && revision == 1 && equipment == equipmentId);

        Assert.True((await runtime.ActivateAsync("eee", 1, package)).Activated);
        var alarm = Assert.Single(runtime.AlarmDefinitions());
        Assert.Equal("Intelligence", alarm.Area);
        Assert.Equal(targetId, alarm.TagId);

        await runtime.WriteAsync(targetId, 9);
        await runtime.WriteAsync(conditionId, 1);
        Assert.Empty(changes);

        permitted = true;
        await runtime.WriteAsync(conditionId, 0);
        Assert.Empty(changes);
        await runtime.WriteAsync(conditionId, 1);
        await runtime.WriteAsync(targetId, 10);
        Assert.Equal(AlarmState.Active, Assert.Single(changes).Current.State);
        Assert.Equal(9, changes[0].Current.LastValue);

        await runtime.WriteAsync(targetId, 7);
        Assert.Equal(AlarmState.Returned, changes[1].Current.State);
        active = false;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.WriteAsync(targetId, 9));
        Assert.Equal(2, changes.Count);
    }

    private static EngineeringRuntimeCoordinator CreateRuntime(IServerMemoryRetentionStore retention) =>
        new(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1),
            retention);

    private static EngineeringPackage ServerPackage(Guid tagId, int initialValue) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "Counter",
                    "Plant.Counter",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: Initial(TagDataType.Int32, initialValue))
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });

    private static MemoryInitialValueDto Initial(TagDataType type, object value) =>
        new(type, JsonSerializer.SerializeToElement(value, value.GetType()));
}
