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

public sealed class EngineeringRuntimeInternalMemoryIntegrationTests
{
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
