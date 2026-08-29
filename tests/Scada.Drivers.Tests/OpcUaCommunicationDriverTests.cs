using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaCommunicationDriverTests
{
    [Fact]
    public async Task Subscription_PreservesQualityAndProtocolTimestampsInCache()
    {
        var session = new FakeSession();
        var factory = new FakeFactory(session);
        await using var driver = new OpcUaCommunicationDriver("opcua-1", "OPC UA Test", factory);
        var tag = CreateTag(readOnly: false);
        var sourceTime = DateTimeOffset.UtcNow.AddSeconds(-2);
        var serverTime = DateTimeOffset.UtcNow.AddSeconds(-1);

        await driver.StartAsync([tag], CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var updates = driver.SubscribeAsync(timeout.Token).GetAsyncEnumerator();
        var next = updates.MoveNextAsync().AsTask();

        session.Publish(new OpcUaRuntimeDataValue(tag.Id, 42.5d, TagQuality.Uncertain, sourceTime, serverTime));

        Assert.True(await next);
        Assert.Equal(tag.Id, updates.Current.TagId);
        Assert.Equal(42.5d, updates.Current.Value);
        Assert.Equal(TagQuality.Uncertain, updates.Current.Quality);
        Assert.Equal(sourceTime, updates.Current.SourceTimestamp);
        Assert.Equal(serverTime, updates.Current.ServerTimestamp);
        Assert.Equal("opcua-1", updates.Current.Source);

        var cached = await driver.ReadAsync(tag, timeout.Token);
        Assert.Equal(updates.Current, cached);
        Assert.Equal(DriverState.Running, driver.Status.State);
    }

    [Fact]
    public async Task Write_ValidatesCanonicalTypeBeforeCallingTransport()
    {
        var session = new FakeSession();
        var factory = new FakeFactory(session);
        await using var driver = new OpcUaCommunicationDriver("opcua-1", "OPC UA Test", factory);
        var writable = CreateTag(readOnly: false);
        var readOnly = CreateTag(readOnly: true);

        await driver.StartAsync([writable, readOnly], CancellationToken.None);
        await driver.WriteAsync(writable, 12.25d, CancellationToken.None);

        Assert.True(session.LastWrite.HasValue);
        Assert.Equal(writable.Id, session.LastWrite.Value.Binding.Tag.Id);
        Assert.Equal(12.25d, Assert.IsType<double>(session.LastWrite.Value.Value));
        await Assert.ThrowsAsync<ArgumentException>(
            () => driver.WriteAsync(writable, 12.25f, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.WriteAsync(readOnly, 12.25d, CancellationToken.None));
        Assert.Equal(1, session.WriteCalls);
    }

    [Fact]
    public async Task Supervisor_RetriesWithBoundedPolicyWithoutLeakingSdkTypes()
    {
        var session = new FakeSession();
        var factory = new RetryFactory(session);
        await using var supervisor = new OpcUaRuntimeSessionSupervisor(factory, [TimeSpan.Zero]);
        var tag = CreateTag(readOnly: false);
        var binding = OpcUaRuntimeBinding.FromTag(tag);
        var failedAttempts = new List<int>();

        var connected = await supervisor.ReconnectUntilAvailableAsync(
            [binding],
            failedAttempts.Add,
            CancellationToken.None);

        Assert.Same(session, connected);
        Assert.Equal(2, factory.ConnectCalls);
        Assert.Equal(new[] { 1 }, failedAttempts);
    }

    private static TagDefinition CreateTag(bool readOnly) => TagDefinition.Create(
        name: "Value",
        path: $"Area.Value.{Guid.NewGuid():N}",
        dataType: TagDataType.Double,
        readOnly: readOnly,
        metadata: new Dictionary<string, string>
        {
            ["opcUa.nodeId"] = $"ns=2;s=Value.{Guid.NewGuid():N}",
            ["opcUa.namespaceUri"] = "urn:elite:test"
        });

    private sealed class FakeFactory(IOpcUaRuntimeSession session) : IOpcUaRuntimeSessionFactory
    {
        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(session);
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
            _updates.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
