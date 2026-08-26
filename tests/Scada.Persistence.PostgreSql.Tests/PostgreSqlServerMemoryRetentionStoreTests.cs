using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlServerMemoryRetentionStoreTests
{
    public static IEnumerable<object[]> TypedValues()
    {
        yield return [TagDataType.Boolean, true];
        yield return [TagDataType.Int16, (short)12];
        yield return [TagDataType.Int32, 123];
        yield return [TagDataType.Int64, 123L];
        yield return [TagDataType.Float, 12.5F];
        yield return [TagDataType.Double, 12.5D];
        yield return [TagDataType.String, "retained"];
        yield return [TagDataType.DateTime, new DateTimeOffset(2026, 8, 26, 12, 34, 56, TimeSpan.Zero)];
        yield return [TagDataType.Enum, 7];
    }

    [Theory]
    [MemberData(nameof(TypedValues))]
    public async Task Store_RoundTripsExactTypedValueAcrossStoreRestart(TagDataType dataType, object expected)
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tagId = Guid.NewGuid();
        var storedAt = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        await using (var first = new PostgreSqlServerMemoryRetentionStore(connectionString))
        {
            await first.InitializeAsync();
            await first.InitializeAsync();
            await first.WriteAsync(new RetainedMemoryValue(
                tagId,
                new TypedTagValue(dataType, expected),
                storedAt));
        }

        await using var restarted = new PostgreSqlServerMemoryRetentionStore(connectionString);
        await restarted.InitializeAsync();
        var restored = await restarted.ReadAsync(tagId);

        Assert.NotNull(restored);
        Assert.Equal(dataType, restored!.TypedValue.DataType);
        Assert.IsType(expected.GetType(), restored.TypedValue.Value);
        Assert.Equal(expected, restored.TypedValue.Value);
        Assert.Equal(storedAt, restored.StoredAt);

        await restarted.DeleteAsync(tagId);
        Assert.Null(await restarted.ReadAsync(tagId));
    }

    [Fact]
    public async Task Provider_RestartAndPathRename_PreserveRetainedValueByStableTagId()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tagId = Guid.NewGuid();
        var original = CreateTag(tagId, "Memory.Counter.Old", TagDataType.Int32);
        var renamed = CreateTag(tagId, "Memory.Counter.Renamed", TagDataType.Int32);

        await using (var store = new PostgreSqlServerMemoryRetentionStore(connectionString))
        {
            await store.InitializeAsync();
            var runtime = new ServerMemorySourceProvider("memory.server.main", store);
            await runtime.ActivateAsync([new MemoryTagDefinition(original)]);
            await runtime.WriteAsync(tagId, 91);
        }

        await using var restartedStore = new PostgreSqlServerMemoryRetentionStore(connectionString);
        await restartedStore.InitializeAsync();
        var restartedRuntime = new ServerMemorySourceProvider("memory.server.main", restartedStore);
        await restartedRuntime.ActivateAsync([new MemoryTagDefinition(renamed)]);

        Assert.Equal("Memory.Counter.Renamed", restartedRuntime.Tags.Single().Path);
        Assert.Equal(91, Assert.IsType<int>(Assert.IsType<TagValue>(await restartedRuntime.ReadAsync(tagId)).Value));
        await restartedStore.DeleteAsync(tagId);
    }

    [Fact]
    public async Task IncompatibleRetainedType_RequiresExplicitResetBeforeNewTypeCanActivate()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tagId = Guid.NewGuid();
        var intTag = CreateTag(tagId, "Memory.Value", TagDataType.Int32);
        var doubleTag = CreateTag(tagId, "Memory.Value", TagDataType.Double);

        await using var store = new PostgreSqlServerMemoryRetentionStore(connectionString);
        await store.InitializeAsync();
        var provider = new ServerMemorySourceProvider("memory.server.main", store);
        await provider.ActivateAsync([
            new MemoryTagDefinition(intTag, new TypedTagValue(TagDataType.Int32, 1))
        ]);
        await provider.WriteAsync(tagId, 12);

        await Assert.ThrowsAsync<MemoryRetentionTypeMismatchException>(() =>
            provider.ActivateAsync([
                new MemoryTagDefinition(doubleTag, new TypedTagValue(TagDataType.Double, 2.5D))
            ]).AsTask());

        await provider.ResetRetainedValueAsync(tagId);
        Assert.Null(await store.ReadAsync(tagId));

        await provider.ActivateAsync([
            new MemoryTagDefinition(doubleTag, new TypedTagValue(TagDataType.Double, 2.5D))
        ]);
        Assert.Equal(2.5D, Assert.IsType<double>(Assert.IsType<TagValue>(await provider.ReadAsync(tagId)).Value));
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
