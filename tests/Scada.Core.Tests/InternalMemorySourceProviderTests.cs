using Scada.Core.InternalMemory;
using Scada.Core.Sources;
using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class InternalMemorySourceProviderTests
{
    [Fact]
    public async Task Server_memory_uses_typed_initial_value_with_good_quality_and_no_network_transport()
    {
        var tag = CreateTag(TagDataType.Int32);
        var provider = new ServerMemorySourceProvider(
            "memory.server.main",
            new InMemoryServerMemoryRetentionStore());

        await provider.ActivateAsync([
            new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Int32, 17))
        ]);

        var current = Assert.IsType<TagValue>(await provider.ReadAsync(tag.Id));
        Assert.Equal(17, Assert.IsType<int>(current.Value));
        Assert.Equal(TagQuality.Good, current.Quality);
        Assert.Equal("memory.server.main", current.Source);
        Assert.Equal("builtin.memory.server", provider.Descriptor.TypeKey);
        Assert.Equal(SourceProviderOwnerScope.Server, provider.Descriptor.OwnerScope);
        Assert.True(provider.Descriptor.Retentive);
        Assert.False(provider.Descriptor.HasNetworkTransport);
    }

    [Fact]
    public async Task Server_memory_restores_last_retained_value_after_provider_restart()
    {
        var tag = CreateTag(TagDataType.Int32);
        var definition = new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Int32, 3));
        var retention = new InMemoryServerMemoryRetentionStore();

        var firstRuntime = new ServerMemorySourceProvider("memory.server.main", retention);
        await firstRuntime.ActivateAsync([definition]);
        await firstRuntime.WriteAsync(tag.Id, 42);

        var restartedRuntime = new ServerMemorySourceProvider("memory.server.main", retention);
        await restartedRuntime.ActivateAsync([definition]);

        var restored = Assert.IsType<TagValue>(await restartedRuntime.ReadAsync(tag.Id));
        Assert.Equal(42, Assert.IsType<int>(restored.Value));
        Assert.Equal(TagQuality.Good, restored.Quality);
    }

    [Fact]
    public async Task Server_memory_retention_follows_stable_tag_id_across_path_rename()
    {
        var tagId = Guid.NewGuid();
        var original = CreateTag(TagDataType.Int32, tagId, "Server.Counter.Old");
        var renamed = CreateTag(TagDataType.Int32, tagId, "Server.Counter.Renamed");
        var retention = new InMemoryServerMemoryRetentionStore();

        var firstRuntime = new ServerMemorySourceProvider("memory.server.main", retention);
        await firstRuntime.ActivateAsync([new MemoryTagDefinition(original)]);
        await firstRuntime.WriteAsync(tagId, 91);

        var nextRevision = new ServerMemorySourceProvider("memory.server.main", retention);
        await nextRevision.ActivateAsync([new MemoryTagDefinition(renamed)]);

        Assert.Equal("Server.Counter.Renamed", nextRevision.Tags.Single().Path);
        var restored = Assert.IsType<TagValue>(await nextRevision.ReadAsync(tagId));
        Assert.Equal(91, Assert.IsType<int>(restored.Value));
    }

    [Fact]
    public async Task Incompatible_retained_type_fails_closed_without_coercion_or_state_replacement()
    {
        var tagId = Guid.NewGuid();
        var intTag = CreateTag(TagDataType.Int32, tagId, "Server.Value");
        var doubleTag = CreateTag(TagDataType.Double, tagId, "Server.Value");
        var retention = new InMemoryServerMemoryRetentionStore();
        var provider = new ServerMemorySourceProvider("memory.server.main", retention);

        await provider.ActivateAsync([new MemoryTagDefinition(intTag)]);
        await provider.WriteAsync(tagId, 12);

        var exception = await Assert.ThrowsAsync<MemoryRetentionTypeMismatchException>(
            () => provider.ActivateAsync([
                new MemoryTagDefinition(doubleTag, new TypedTagValue(TagDataType.Double, 1D))
            ]).AsTask());

        Assert.Equal(tagId, exception.TagId);
        Assert.Equal(TagDataType.Int32, exception.RetainedType);
        Assert.Equal(TagDataType.Double, exception.ActiveType);
        Assert.Equal(TagDataType.Int32, provider.Tags.Single().DataType);

        var stillActive = Assert.IsType<TagValue>(await provider.ReadAsync(tagId));
        Assert.Equal(12, Assert.IsType<int>(stillActive.Value));
    }

    [Fact]
    public async Task Removed_tag_is_not_resurrected_by_stale_retention()
    {
        var tag = CreateTag(TagDataType.Int32);
        var retention = new InMemoryServerMemoryRetentionStore();
        var provider = new ServerMemorySourceProvider("memory.server.main", retention);

        await provider.ActivateAsync([new MemoryTagDefinition(tag)]);
        await provider.WriteAsync(tag.Id, 55);
        Assert.Single(retention.Snapshot());

        await provider.ActivateAsync(Array.Empty<MemoryTagDefinition>());

        Assert.Empty(provider.Tags);
        Assert.Null(await provider.ReadAsync(tag.Id));
        Assert.Single(retention.Snapshot());

        var restartedWithoutTag = new ServerMemorySourceProvider("memory.server.main", retention);
        await restartedWithoutTag.ActivateAsync(Array.Empty<MemoryTagDefinition>());
        Assert.Null(await restartedWithoutTag.ReadAsync(tag.Id));
    }

    [Fact]
    public async Task Server_memory_write_rejects_wrong_runtime_type_without_silent_numeric_coercion()
    {
        var tag = CreateTag(TagDataType.Int32);
        var retention = new InMemoryServerMemoryRetentionStore();
        var provider = new ServerMemorySourceProvider("memory.server.main", retention);
        await provider.ActivateAsync([
            new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Int32, 7))
        ]);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.WriteAsync(tag.Id, 7L).AsTask());

        var current = Assert.IsType<TagValue>(await provider.ReadAsync(tag.Id));
        Assert.Equal(7, Assert.IsType<int>(current.Value));
        Assert.Empty(retention.Snapshot());
    }

    [Fact]
    public async Task Client_memory_is_isolated_per_runtime_client_and_new_client_restarts_from_initial_value()
    {
        var tag = CreateTag(TagDataType.String);
        var definition = new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.String, "initial"));
        var factory = new ClientMemorySourceProviderFactory();

        var clientA = factory.Create("memory.client.ui", "runtime-client-A", [definition]);
        var clientB = factory.Create("memory.client.ui", "runtime-client-B", [definition]);

        await clientA.WriteAsync(tag.Id, "A-only");

        Assert.Equal("A-only", Assert.IsType<string>(Assert.IsType<TagValue>(await clientA.ReadAsync(tag.Id)).Value));
        Assert.Equal("initial", Assert.IsType<string>(Assert.IsType<TagValue>(await clientB.ReadAsync(tag.Id)).Value));
        Assert.Equal("runtime-client-A", clientA.RuntimeClientId);
        Assert.Equal("runtime-client-B", clientB.RuntimeClientId);
        Assert.Equal(SourceProviderOwnerScope.RuntimeClient, clientA.Descriptor.OwnerScope);
        Assert.False(clientA.Descriptor.Retentive);
        Assert.False(clientA.Descriptor.HasNetworkTransport);
        Assert.False(clientA.Descriptor.SupportsGlobalHistorianAndAlarms);

        var newClient = factory.Create("memory.client.ui", "runtime-client-C", [definition]);
        Assert.Equal("initial", Assert.IsType<string>(Assert.IsType<TagValue>(await newClient.ReadAsync(tag.Id)).Value));
    }

    [Fact]
    public void Typed_defaults_are_deterministic_and_preserve_exact_runtime_types()
    {
        Assert.False(Assert.IsType<bool>(TypedTagValue.CreateDefault(TagDataType.Boolean).Value));
        Assert.Equal((short)0, Assert.IsType<short>(TypedTagValue.CreateDefault(TagDataType.Int16).Value));
        Assert.Equal(0, Assert.IsType<int>(TypedTagValue.CreateDefault(TagDataType.Int32).Value));
        Assert.Equal(0L, Assert.IsType<long>(TypedTagValue.CreateDefault(TagDataType.Int64).Value));
        Assert.Equal(0F, Assert.IsType<float>(TypedTagValue.CreateDefault(TagDataType.Float).Value));
        Assert.Equal(0D, Assert.IsType<double>(TypedTagValue.CreateDefault(TagDataType.Double).Value));
        Assert.Equal(string.Empty, Assert.IsType<string>(TypedTagValue.CreateDefault(TagDataType.String).Value));
        Assert.Equal(DateTimeOffset.UnixEpoch, Assert.IsType<DateTimeOffset>(TypedTagValue.CreateDefault(TagDataType.DateTime).Value));
        Assert.Equal(0, Assert.IsType<int>(TypedTagValue.CreateDefault(TagDataType.Enum).Value));
    }

    [Fact]
    public void Memory_tag_initial_value_rejects_mismatched_declared_type()
    {
        var tag = CreateTag(TagDataType.Int32);

        Assert.Throws<ArgumentException>(() =>
            new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Int64, 1L)));
    }

    private static TagDefinition CreateTag(
        TagDataType dataType,
        Guid? id = null,
        string path = "Server.Memory.Value",
        bool readOnly = false) =>
        new(
            id ?? Guid.NewGuid(),
            path.Split('.').Last(),
            path,
            dataType,
            Source: "memory.source",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: readOnly);
}
