using System.Collections.Concurrent;
using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaCommunicationDiagnosticsTests
{
    [Fact]
    public async Task Diagnostics_TrackLifecycleQualityWritesAndSanitizeEndpoint()
    {
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        var factory = new SessionFactory();
        var tag = CreateTag();
        await using var driver = new OpcUaCommunicationDriver(
            "opc-ua:line-1",
            "Line 1 OPC UA",
            cache,
            registry,
            [tag],
            factory,
            [TimeSpan.Zero],
            "opc.tcp://user:password@server:4840/plant?token=secret#fragment",
            TimeSpan.FromMilliseconds(250));

        var stopped = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Stopped, stopped.State);
        Assert.Equal(1, stopped.TagQuality.NoCurrentSample);
        Assert.True(driver.Capabilities.HasFlag(DriverCapabilities.Diagnostics));
        Assert.NotNull(stopped.Endpoint);
        Assert.DoesNotContain("user", stopped.Endpoint!);
        Assert.DoesNotContain("password", stopped.Endpoint!);
        Assert.DoesNotContain("token", stopped.Endpoint!);
        Assert.DoesNotContain("secret", stopped.Endpoint!);

        await driver.StartAsync();
        var session = Assert.Single(factory.Sessions);
        var healthy = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, healthy.State);
        Assert.Equal(1, healthy.Counters.Connections);
        Assert.Equal(TimeSpan.FromMilliseconds(250), healthy.ConfiguredScanInterval);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        session.Publish(new OpcUaRuntimeDataValue(tag.Id, 17.5d, TagQuality.Good));
        await cache.ReadNextAsync(timeout.Token);
        await driver.WriteAsync(tag.Id, 18.5d, timeout.Token);

        var active = driver.GetCommunicationDiagnostics();
        Assert.Equal(1, active.TagQuality.Good);
        Assert.Equal(1, active.Counters.WriteOperations);
        Assert.Equal(1, active.Counters.Requests);
        Assert.True(active.Counters.UpdatesPublished >= 2);
        Assert.True(active.Counters.SuccessfulOperations >= 3);
        Assert.NotNull(active.LastSuccessfulCommunicationAt);
        Assert.NotNull(active.ProtocolDetails);
        Assert.Equal("Subscription", active.ProtocolDetails!["acquisitionMode"]);
        Assert.Equal("driverInitiatedWritesOnly", active.ProtocolDetails["requestCounterScope"]);
        Assert.Equal("250", active.ProtocolDetails["publishingIntervalMs"]);

        await driver.StopAsync();
        var final = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Stopped, final.State);
        Assert.Equal(1, final.Counters.Disconnections);
    }

    private static TagDefinition CreateTag() => TagDefinition.Create(
        name: "Value",
        path: "Area.Line1.Value",
        dataType: TagDataType.Double,
        readOnly: false,
        metadata: new Dictionary<string, string>
        {
            [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=2;s=Value",
            [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = "urn:line:1"
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

    private sealed class FakeSession : IOpcUaRuntimeSession
    {
        private readonly Channel<OpcUaRuntimeDataValue> _updates = Channel.CreateUnbounded<OpcUaRuntimeDataValue>();

        public void Publish(OpcUaRuntimeDataValue value) => _updates.Writer.TryWrite(value);

        public Task<OpcUaRuntimeDataValue> ReadAsync(
            OpcUaRuntimeBinding binding,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OpcUaRuntimeDataValue(binding.Tag.Id, 0d, TagQuality.Good));

        public Task WriteAsync(
            OpcUaRuntimeBinding binding,
            object value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(CancellationToken cancellationToken) =>
            _updates.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
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
            _tags[tag.Id] = tag;
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
