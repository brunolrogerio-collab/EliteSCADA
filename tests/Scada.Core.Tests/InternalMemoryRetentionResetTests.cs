using Scada.Core.InternalMemory;
using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class InternalMemoryRetentionResetTests
{
    [Fact]
    public async Task ExplicitReset_AllowsLaterIncompatibleTypeActivationWithoutCoercion()
    {
        var tagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        var provider = new ServerMemorySourceProvider("memory.server.main", retention);
        var intTag = CreateTag(tagId, "Memory.Value", TagDataType.Int32);
        var doubleTag = CreateTag(tagId, "Memory.Value", TagDataType.Double);

        await provider.ActivateAsync([
            new MemoryTagDefinition(intTag, new TypedTagValue(TagDataType.Int32, 2))
        ]);
        await provider.WriteAsync(tagId, 12);

        await Assert.ThrowsAsync<MemoryRetentionTypeMismatchException>(() =>
            provider.ActivateAsync([
                new MemoryTagDefinition(doubleTag, new TypedTagValue(TagDataType.Double, 2.5D))
            ]).AsTask());

        await provider.ResetRetainedValueAsync(tagId);
        Assert.Null(await retention.ReadAsync(tagId));
        Assert.Equal(2, Assert.IsType<int>(Assert.IsType<TagValue>(await provider.ReadAsync(tagId)).Value));

        await provider.ActivateAsync([
            new MemoryTagDefinition(doubleTag, new TypedTagValue(TagDataType.Double, 2.5D))
        ]);

        Assert.Equal(2.5D, Assert.IsType<double>(Assert.IsType<TagValue>(await provider.ReadAsync(tagId)).Value));
    }

    [Fact]
    public async Task ExplicitReset_RejectsInactiveTagWithoutDeletingDurableState()
    {
        var activeTagId = Guid.NewGuid();
        var inactiveTagId = Guid.NewGuid();
        var retention = new InMemoryServerMemoryRetentionStore();
        var provider = new ServerMemorySourceProvider("memory.server.main", retention);

        await retention.WriteAsync(new RetainedMemoryValue(
            inactiveTagId,
            new TypedTagValue(TagDataType.Int32, 99),
            DateTimeOffset.UtcNow));

        await provider.ActivateAsync([
            new MemoryTagDefinition(
                CreateTag(activeTagId, "Memory.Active", TagDataType.Int32),
                new TypedTagValue(TagDataType.Int32, 1))
        ]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            provider.ResetRetainedValueAsync(inactiveTagId).AsTask());

        var retained = Assert.IsType<RetainedMemoryValue>(await retention.ReadAsync(inactiveTagId));
        Assert.Equal(TagDataType.Int32, retained.TypedValue.DataType);
        Assert.Equal(99, Assert.IsType<int>(retained.TypedValue.Value));
    }

    private static TagDefinition CreateTag(Guid id, string path, TagDataType dataType) =>
        new(
            id,
            path.Split('.').Last(),
            path,
            dataType,
            Source: "memory.server.main",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: false);
}
