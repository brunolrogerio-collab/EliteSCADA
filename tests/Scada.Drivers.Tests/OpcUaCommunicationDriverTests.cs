using System.Collections.Concurrent;
using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaCommunicationDriverTests
{
    [Fact]
    public async Task Subscription_PublishesToCanonicalCacheWithProtocolTimestamps()
    {
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        var factory = new SessionFactory();
        var tag = CreateTag(readOnly: false);
        await using var driver = CreateDriver(cache, registry, factory, [tag]);
        var sourceTime = DateTimeOffset.UtcNow.AddSeconds(-2);
        var serverTime = DateTimeOffset.UtcNow.AddSeconds(-1);

        await driver.StartAsync();
        var session = Assert.Single(factory.Sessions);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        session.Publish(new OpcUaRuntimeDataValue(tag.Id, 42.5d, TagQuality.Uncertain, sourceTime, serverTime));

        var update = await cache.ReadNextAsync(timeout.Token);
        Assert.Equal(tag.Id, update.TagId);
        Assert.Equal(42.5d, Assert.IsType<double>(update.Value));
        Assert.Equal(TagQuality.Uncertain, update.Quality);
        Assert.Equal(sourceTime, update.SourceTimestamp);
        Assert.Equal(serverTime, update.ServerTimestamp);
        Assert.Equal("opcua-1", update.Source);
        var cached = await driver.ReadAsync(tag.Id, timeout.Token);
        Assert.NotNull(cached);
        Assert.Equal(update, cached);
        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.Contains(registry.Snapshot(), x => x.Id == tag.Id);
    }

    [Fact]
    public async Task Write_ValidatesBeforeTransportAndUpdatesCanonicalCache()
    {
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        var factory = new SessionFactory();
        var writable = CreateTag(readOnly: false);
        var readOnly = CreateTag(readOnly: true);
        await using var driver = CreateDriver(cache, registry, factory, [writable, readOnly]);

        await driver.StartAsync();
        var session = Assert.Single(factory.Sessions);
        await driver.WriteAsync(writable.Id, 12.25d);

        var cached = await driver.ReadAsync(writable.Id);
        Assert.NotNull(cached);
        Assert.Equal(12.25d, Assert.IsType<double>(cached.Value));
        Assert.Equal(TagQuality.Good, cached.Quality);
        Assert.Equal(1, session.WriteCalls);
        Assert.True(session.LastWrite.HasValue);
        Assert.Equal(writable.Id, session.LastWrite.Value.Binding.Tag.Id);

        await Assert.ThrowsAsync<ArgumentException>(
            () => driver.WriteAsync(writable.Id, 12.25f).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.WriteAsync(readOnly.Id, 12.25d).AsTask());
        Assert.Equal(1, session.WriteCalls);
    }

    [Fact]
    public async Task UnknownTag_IsRejectedAndStopThenStartCreatesFreshSession()
    {
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        var factory = new SessionFactory();
        var tag = CreateTag(readOnly: false);
        await using var driver = CreateDriver(cache, registry, factory, [tag]);
        var unknown = Guid.NewGuid();

        await driver.StartAsync();
        var first = Assert.Single(factory.Sessions);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => driver.ReadAsync(unknown).AsTask());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => driver.WriteAsync(unknown, 1d).AsTask());

        await driver.StopAsync();
        Assert.True(first.Disposed);
        Assert.Equal(DriverState.Stopped, driver.Status.State);

        await driver.StartAsync();
        Assert.Equal(2, factory.Sessions.Count);
        Assert.NotSame(first, factory.Sessions[1]);
        Assert.Equal(DriverState.Running, driver.Status.State);
    }

    [Fact]
    public async Task Supervisor_RetriesUsingConfiguredBoundedDelayPolicy()
    {
        var session = new FakeSession();
        var factory = new RetryFactory(session);
        await using var supervisor = new OpcUaRuntimeSessionSupervisor(factory, [TimeSpan.Zero]);
        var binding = OpcUaRuntimeBinding.FromTag(CreateTag(readOnly: false));
        var failedAttempts = new List<int>();

        var connected = await supervisor.ReconnectUntilAvailableAsync(
            [binding], failedAttempts.Add, CancellationToken.None);

        Assert.Same(session, connected);
        Assert.Equal(2, factory.ConnectCalls);
        Assert.Equal(new[] { 1 }, failedAttempts);
    }

    private static OpcUaCommunicationDriver CreateDriver(
        ICurrentTagCache cache,
        ITagRegistry registry,
        IOpcUaRuntimeSessionFactory factory,
        IReadOnlyCollection<TagDefinition> tags) =>
        new("opcua-1", "OPC UA Test", cache, registry, tags, factory, [TimeSpan.Zero]);

    private static TagDefinition CreateTag(bool readOnly) => TagDefinition.Create(
        name: "Value",
        path: $"Area.Value.{Guid.NewGuid():N}",
        dataType: TagDataType.Double,
        readOnly: readOnly,
        metadata: new Dictionary<string, string>
        {
            [OpcUaRuntimeBinding.NodeIdMetadataKey] = $"ns=2;s=Value.{Guid.NewGuid():N}",
            [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = "urn:elite:test"
        });

    private sealed class SessionFactory : IOpcUaRuntimeSessionFactory
    {
        public List<FakeSession> Sessions { get; } = [];

        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = new FakeSession();
            Sessions.Add(session);
            return Task.FromResult<IOpcUaRuntimeSession>(session);
        }
    }

    private sealed class RetryFactory(IOpcUaRuntimeSession session) : IOpcUaRuntimeSessionFactory
    {
        public int ConnectCalls { get; private set; }

        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectCalls++;
            if (ConnectCalls == 1) throw new InvalidOperationException("simulated connection failure");
            return Task.FromResult(session);
        }
    }

    private sealed class FakeSession : IOpcUaRuntimeSession
    {
        private readonly Channel<OpcUaRuntimeDataValue> _updates = Channel.CreateUnbounded<OpcUaRuntimeDataValue>();
        public (OpcUaRuntimeBinding Binding, object Value)? LastWrite { get; private set; }
        public int WriteCalls { get; private set; }
        public bool Disposed { get; private set; }

        public void Publish(OpcUaRuntimeDataValue value) => _updates.Writer.TryWrite(value);

        public Task<OpcUaRuntimeDataValue> ReadAsync(
            OpcUaRuntimeBinding binding,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OpcUaRuntimeDataValue(binding.Tag.Id, 0d, TagQuality.Good));
        }

        public Task WriteAsync(
            OpcUaRuntimeBinding binding,
            object value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCalls++;
            LastWrite = (binding, value);
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(CancellationToken cancellationToken) =>
            _updates.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _updates.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCache : ICurrentTagCache
    {
        private readonly ConcurrentDictionary<Guid, TagValue> _values = new();
        private readonly Channel<TagValue> _updates = Channel.CreateUnbounded<TagValue>();

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            var found = _values.TryGetValue(tagId, out var current);
            value = current;
            return found;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.TryGetValue(tag.Id, out var previous);
            _values[tag.Id] = value;
            _updates.Writer.TryWrite(value);
            return ValueTask.FromResult<TagValue?>(previous);
        }

        public ValueTask<TagValue> ReadNextAsync(CancellationToken cancellationToken) =>
            _updates.Reader.ReadAsync(cancellationToken);
    }

    private sealed class FakeRegistry : ITagRegistry
    {
        private readonly Dictionary<Guid, TagDefinition> _tags = [];

        public TagDefinition Register(TagDefinition tag)
        {
            _tags.Add(tag.Id, tag);
            return tag;
        }

        public TagDefinition Upsert(TagDefinition tag)
        {
            _tags[tag.Id] = tag;
            return tag;
        }

        public bool TryGet(Guid tagId, out TagDefinition? tag) => _tags.TryGetValue(tagId, out tag);

        public bool TryGetByPath(string path, out TagDefinition? tag)
        {
            tag = _tags.Values.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            return tag is not null;
        }

        public IReadOnlyCollection<TagDefinition> Snapshot() => _tags.Values.ToArray();
    }
}
