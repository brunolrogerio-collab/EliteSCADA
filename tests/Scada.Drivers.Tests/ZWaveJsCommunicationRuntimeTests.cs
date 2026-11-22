using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ZWaveJsCommunicationRuntimeTests
{
    [Fact]
    public void Planner_accepts_canonical_commissioned_value_and_rejects_access_control_value()
    {
        Assert.Equal(DriverConnectionModel.HostSerial, new ZWaveJsDriverDescriptorProvider().Descriptor.ConnectionModel);
        var planner = new ZWaveJsCommunicationRuntimePlanner();
        var validAddress = "node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-";
        var validPackage = Package(validAddress);
        var valid = planner.Plan(validPackage, Assert.Single(validPackage.DataSources!));
        Assert.True(valid.CanActivate);
        var point = Assert.Single(Assert.IsType<ZWaveJsCommunicationRuntimePlan>(valid.Plan).Points);
        Assert.Equal(5, point.NodeId);
        Assert.Equal(37, point.CommandClass);
        Assert.True(point.Writable);

        var invalidPackage = Package("node/5/endpoint/0/cc/98/property/string/currentValue/key/none/-");
        var outOfScope = planner.Plan(invalidPackage, Assert.Single(invalidPackage.DataSources!));
        Assert.False(outOfScope.CanActivate);
        Assert.Contains(outOfScope.Issues, issue => issue.Code == "ZWAVE_JS_TAG_ADDRESS_INVALID" && issue.IsError);
    }

    [Fact]
    public async Task Standby_reads_cache_without_starting_or_reading_the_controller()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        await cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, false, "test"));
        var registry = new InMemoryTagRegistry();
        var session = new FakeSession();
        var services = new CommunicationDriverRuntimeServices("project", cache, registry, EffectAuthority: () => false);
        await using var driver = new ZWaveJsCommunicationDriver(RuntimePlan(point), services, session, "US");

        var current = await driver.ReadAsync(point.Tag.Id);

        Assert.Equal(false, current?.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.StartAsync());
        Assert.Equal(0, session.StartCount);
        Assert.Equal(0, session.ReadCount);
    }

    [Fact]
    public async Task Initial_controller_snapshot_emitted_during_start_is_published_after_activation()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        var registry = new InMemoryTagRegistry();
        var initialValue = JsonDocument.Parse("true").RootElement.Clone();
        var session = new FakeSession
        {
            StartObservation = new ZWaveJsObservation(point.Tag.Id, initialValue, DateTimeOffset.UtcNow)
        };
        var services = new CommunicationDriverRuntimeServices("project", cache, registry, EffectAuthority: () => true);
        await using var driver = new ZWaveJsCommunicationDriver(RuntimePlan(point), services, session, "US");

        await driver.StartAsync();

        Assert.True(cache.TryGet(point.Tag.Id, out var current));
        Assert.Equal(true, current?.Value);
        Assert.Equal(TagQuality.Good, current?.Quality);
    }

    [Fact]
    public async Task Ambiguous_write_keeps_last_value_but_marks_quality_uncertain_without_retry()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        await cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, false, "test"));
        var registry = new InMemoryTagRegistry();
        var session = new FakeSession { WriteException = new ZWaveJsWriteUnconfirmedException(point.Tag.Id, "WRITE_NOT_CONFIRMED") };
        var services = new CommunicationDriverRuntimeServices("project", cache, registry, EffectAuthority: () => true);
        await using var driver = new ZWaveJsCommunicationDriver(RuntimePlan(point), services, session, "US");
        await driver.StartAsync();

        await Assert.ThrowsAsync<ZWaveJsWriteUnconfirmedException>(() => driver.WriteAsync(point.Tag.Id, true).AsTask());

        Assert.True(cache.TryGet(point.Tag.Id, out var current));
        Assert.Equal(false, current?.Value);
        Assert.Equal(TagQuality.Uncertain, current?.Quality);
        Assert.Equal(1, session.WriteCount);
    }

    private static EngineeringPackage Package(string address)
    {
        var source = Source();
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(), Name: "Switch", Path: "Home.Switch", DataType: TagDataType.Boolean,
            Source: source.Key, Address: address, ReadOnly: false,
            CommunicationBinding: new CommunicationTagBinding(1, ZWaveJsDriverDescriptorProvider.TagBindingSchemaId, 1, address),
            DataSourceId: source.Id);
        return new EngineeringPackage("scada.engineering", 15, DateTimeOffset.UtcNow, [tag], [], [source]);
    }

    private static DataSourceEngineeringDto Source() => new(
        Guid.NewGuid(), "zwave.main", "Z-Wave Controller", ZWaveJsDriverDescriptorProvider.DriverType,
        Settings: new Dictionary<string, string> { ["hostResourceId"] = "zwave-controller" });

    private static ZWaveJsCommunicationRuntimePlan RuntimePlan(ZWaveJsPoint point) => new(
        "zwave.main", "Z-Wave Controller", "zwave-controller", new Dictionary<string, string>(), [point], Guid.NewGuid());

    private static ZWaveJsPoint RuntimePoint()
    {
        var tag = TagDefinition.Create("Switch", "Home.Switch", TagDataType.Boolean, "zwave.main", readOnly: false);
        return new ZWaveJsPoint(tag, "node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-", 5, 0, 37,
            "currentValue", null, Writable: true);
    }

    private sealed class FakeCache : ICurrentTagCache
    {
        private readonly Dictionary<Guid, TagValue> _values = new();
        public bool TryGet(Guid tagId, out TagValue? value) => _values.TryGetValue(tagId, out value);
        public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();
        public ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue(tag.Id, out var previous);
            _values[tag.Id] = value;
            return ValueTask.FromResult(previous);
        }
    }

    private sealed class FakeSession : IZWaveJsSidecarSession
    {
        private Action<ZWaveJsObservation>? _observationReceived;
        public int StartCount { get; private set; }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public Exception? WriteException { get; init; }
        public ZWaveJsObservation? StartObservation { get; init; }
        public event Action<ZWaveJsObservation>? ObservationReceived { add => _observationReceived += value; remove => _observationReceived -= value; }
        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            if (StartObservation is { } observation) _observationReceived?.Invoke(observation);
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<JsonElement> ReadAsync(ZWaveJsPoint point, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ValueTask.FromResult(JsonDocument.Parse("false").RootElement.Clone());
        }
        public ValueTask<JsonElement> WriteAndReadBackAsync(ZWaveJsPoint point, object value, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return WriteException is null
                ? ValueTask.FromResult(JsonDocument.Parse("true").RootElement.Clone())
                : ValueTask.FromException<JsonElement>(WriteException);
        }
        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
