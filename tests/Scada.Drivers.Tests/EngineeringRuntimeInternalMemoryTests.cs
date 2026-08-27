using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
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
}
