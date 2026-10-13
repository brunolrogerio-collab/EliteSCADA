using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MatterWifiCommunicationRuntimeTests
{
    [Fact]
    public void Planner_accepts_canonical_uint64_node_binding_and_rejects_noncanonical_addresses()
    {
        var planner = new MatterWifiCommunicationRuntimePlanner();
        var validPackage = Package("node/18446744073709551615/endpoint/2/cluster/6/attribute/0");
        var valid = planner.Plan(validPackage, Assert.Single(validPackage.DataSources!));

        Assert.True(valid.CanActivate);
        var point = Assert.Single(Assert.IsType<MatterWifiCommunicationRuntimePlan>(valid.Plan).Points);
        Assert.Equal("18446744073709551615", point.NodeId);
        Assert.Equal("2/6/0", point.PortableAddress);

        var invalidPackage = Package("node/01/endpoint/2/cluster/6/attribute/0");
        var invalid = planner.Plan(invalidPackage, Assert.Single(invalidPackage.DataSources!));
        Assert.False(invalid.CanActivate);
        Assert.Contains(invalid.Issues, issue => issue.Code == "MATTER_WIFI_TAG_ADDRESS_INVALID" && issue.IsError);
    }

    [Fact]
    public void Planner_fails_closed_in_high_availability_installations()
    {
        var package = Package("node/123/endpoint/1/cluster/6/attribute/0");
        var result = new MatterWifiCommunicationRuntimePlanner(() => true)
            .Plan(package, Assert.Single(package.DataSources!));

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, issue => issue.Code == "MATTER_WIFI_HA_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public void High_availability_composition_does_not_advertise_or_register_matter_wifi()
    {
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            highAvailabilityEnabled: () => true,
            matterWifiPreviewEnabled: true);
        Assert.False(components.TryGet(MatterWifiDriverDescriptorProvider.DriverType, out _));
        Assert.DoesNotContain(
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(components).Describe().DataSourceTypes,
            item => item.TypeKey == MatterWifiDriverDescriptorProvider.DriverType);
    }

    [Fact]
    public void Default_composition_does_not_advertise_or_register_matter_even_when_ha_is_off()
    {
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();

        Assert.False(components.TryGet(MatterWifiDriverDescriptorProvider.DriverType, out _));
        Assert.DoesNotContain(
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(components).Describe().DataSourceTypes,
            item => item.TypeKey == MatterWifiDriverDescriptorProvider.DriverType);
    }

    [Fact]
    public void Explicit_preview_composition_contains_matter_driver_and_binding_schema_only_outside_ha()
    {
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(matterWifiPreviewEnabled: true);
        var registration = components.GetRequired(MatterWifiDriverDescriptorProvider.DriverType);
        Assert.IsType<MatterWifiCommunicationRuntimePlanner>(registration.Planner);
        Assert.Equal(MatterWifiDriverDescriptorProvider.DriverType, registration.Factory.DriverType);
        Assert.Equal(MatterWifiDriverDescriptorProvider.TagBindingSchemaId, registration.Descriptor.TagBindingSchemaId);

        var types = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(components).Describe().DataSourceTypes;
        Assert.Contains(types, item => item.TypeKey == MatterWifiDriverDescriptorProvider.DriverType);

        var compilation = new EngineeringDriverCompiler(components).Compile(
            Package("node/123/endpoint/1/cluster/6/attribute/0"));
        Assert.True(compilation.CanActivate);
        Assert.IsType<MatterWifiCommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));
    }

    [Fact]
    public void Matter_preview_requires_explicit_opt_in_and_never_enables_in_production()
    {
        var enabledConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EngineeringDriverCatalogApi.MatterWifiPreviewConfigurationKey] = "true"
            })
            .Build();
        var disabledConfiguration = new ConfigurationBuilder().Build();

        Assert.False(EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled("Development", disabledConfiguration));
        Assert.True(EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled("Development", enabledConfiguration));
        Assert.True(EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled("Test", enabledConfiguration));
        Assert.False(EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled("Production", enabledConfiguration));
        Assert.False(EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled("Staging", enabledConfiguration));
    }

    [Fact]
    public void Direct_factory_is_fail_closed_without_preview_opt_in()
    {
        var resources = new HostResourceRegistry();
        var factory = new MatterWifiCommunicationRuntimeFactory(
            resources,
            new HostResourceLeaseCoordinator(resources),
            new MatterWifiSidecarDeploymentOptions("node", "existing-artifacts", "isolated-state"));
        var services = new CommunicationDriverRuntimeServices("project", new FakeCache(), new InMemoryTagRegistry());

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(RuntimePlan(RuntimePoint()), services));
        Assert.Contains("explicit isolated development/test opt-in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_reports_are_published_and_attribute_writes_require_fresh_readback()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        var registry = new InMemoryTagRegistry();
        var session = new FakeSession
        {
            StartObservation = Observation(point, "true"),
            WriteResult = Json("false")
        };
        var services = new CommunicationDriverRuntimeServices("project", cache, registry, EffectAuthority: () => true);
        await using var driver = new MatterWifiCommunicationDriver(RuntimePlan(point), services, session);

        await driver.StartAsync();
        Assert.True(cache.TryGet(point.Tag.Id, out var startupValue));
        Assert.Equal(true, startupValue?.Value);
        Assert.Equal(CommunicationDriverReadinessState.Ready, driver.GetCommunicationReadiness().State);

        await driver.WriteAsync(point.Tag.Id, false);
        Assert.Equal(1, session.WriteCount);
        Assert.Equal(false, session.LastWriteValue);
        Assert.Equal(false, cache.Snapshot().Single().Value);
    }

    [Fact]
    public async Task Standby_reads_cache_and_never_starts_or_writes_to_matter()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        await cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, true, "test"));
        var session = new FakeSession();
        var services = new CommunicationDriverRuntimeServices("project", cache, new InMemoryTagRegistry(), EffectAuthority: () => false);
        await using var driver = new MatterWifiCommunicationDriver(RuntimePlan(point), services, session);

        var current = await driver.ReadAsync(point.Tag.Id);
        Assert.Equal(true, current?.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.StartAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.WriteAsync(point.Tag.Id, false).AsTask());
        Assert.Equal(0, session.StartCount);
        Assert.Equal(0, session.ReadCount);
        Assert.Equal(0, session.WriteCount);
    }

    [Fact]
    public async Task Unconfirmed_write_keeps_last_value_and_marks_quality_uncertain_without_retry()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        await cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, true, "test"));
        var session = new FakeSession { WriteException = new MatterWifiWriteUnconfirmedException(point.Tag.Id, "WRITE_NOT_CONFIRMED") };
        var services = new CommunicationDriverRuntimeServices("project", cache, new InMemoryTagRegistry(), EffectAuthority: () => true);
        await using var driver = new MatterWifiCommunicationDriver(RuntimePlan(point), services, session);
        await driver.StartAsync();

        await Assert.ThrowsAsync<MatterWifiWriteUnconfirmedException>(() => driver.WriteAsync(point.Tag.Id, false).AsTask());

        Assert.True(cache.TryGet(point.Tag.Id, out var current));
        Assert.Equal(true, current?.Value);
        Assert.Equal(TagQuality.Uncertain, current?.Quality);
        Assert.Equal(1, session.WriteCount);
    }

    [Fact]
    public async Task Incompatible_readback_after_write_preserves_value_and_marks_cache_uncertain()
    {
        var point = RuntimePoint();
        var cache = new FakeCache();
        await cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, true, "test"));
        var session = new FakeSession { WriteResult = Json("\"not-a-boolean\"") };
        var services = new CommunicationDriverRuntimeServices("project", cache, new InMemoryTagRegistry(), EffectAuthority: () => true);
        await using var driver = new MatterWifiCommunicationDriver(RuntimePlan(point), services, session);
        await driver.StartAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => driver.WriteAsync(point.Tag.Id, false).AsTask());

        Assert.True(cache.TryGet(point.Tag.Id, out var current));
        Assert.Equal(true, current?.Value);
        Assert.Equal(TagQuality.Uncertain, current?.Quality);
        Assert.Equal("matter.wifi.write-outcome-unknown", current?.Source);
        Assert.DoesNotContain(cache.Snapshot(), value => value.Quality == TagQuality.Good && value.Source == "matter.confirmed-write");
        Assert.Equal(1, session.WriteCount);
    }

    private static EngineeringPackage Package(string address)
    {
        var source = Source();
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(), Name: "Relay", Path: "Home.Relay", DataType: TagDataType.Boolean,
            Source: source.Key, Address: address, ReadOnly: false,
            CommunicationBinding: new CommunicationTagBinding(1, MatterWifiDriverDescriptorProvider.TagBindingSchemaId, 1, address),
            DataSourceId: source.Id);
        return new EngineeringPackage("scada.engineering", 15, DateTimeOffset.UtcNow, [tag], [], [source]);
    }

    private static DataSourceEngineeringDto Source() => new(
        Guid.NewGuid(), "matter.home", "Matter Home", MatterWifiDriverDescriptorProvider.DriverType,
        SecretReferences: new Dictionary<string, string>
        {
            [MatterWifiDriverDescriptorProvider.FabricStorageKeyField] = HostMaterialSlotReference.Marker
        });

    private static MatterWifiCommunicationRuntimePlan RuntimePlan(MatterWifiPoint point) => new(
        "matter.home", "Matter Home", new Dictionary<string, string>
        {
            [MatterWifiDriverDescriptorProvider.FabricStorageKeyField] = HostMaterialSlotReference.Marker
        }, [point], Guid.NewGuid());

    private static MatterWifiPoint RuntimePoint()
    {
        const string address = "node/123/endpoint/1/cluster/6/attribute/0";
        var binding = new CommunicationTagBinding(1, MatterWifiDriverDescriptorProvider.TagBindingSchemaId, 1, address);
        var tag = TagDefinition.Create("Relay", "Home.Relay", TagDataType.Boolean, "matter.home", readOnly: false) with
        {
            CommunicationBinding = binding
        };
        return new MatterWifiPoint(tag, "123", "1/6/0", Writable: true);
    }

    private static MatterWifiObservation Observation(MatterWifiPoint point, string json) =>
        new(point.Tag.Id, Json(json), DateTimeOffset.UtcNow);

    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();

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

    private sealed class FakeSession : IMatterWifiSidecarSession
    {
        private Action<MatterWifiObservation>? _observationReceived;
        public int StartCount { get; private set; }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public Exception? WriteException { get; init; }
        public MatterWifiObservation? StartObservation { get; init; }
        public JsonElement WriteResult { get; init; } = Json("false");
        public object? LastWriteValue { get; private set; }
        public event Action<MatterWifiObservation>? ObservationReceived { add => _observationReceived += value; remove => _observationReceived -= value; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            if (StartObservation is { } observation) _observationReceived?.Invoke(observation);
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<JsonElement> ReadAsync(MatterWifiPoint point, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ValueTask.FromResult(Json("true"));
        }
        public ValueTask<JsonElement> WriteAndReadBackAsync(MatterWifiPoint point, object value, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            LastWriteValue = value;
            return WriteException is null ? ValueTask.FromResult(WriteResult) : ValueTask.FromException<JsonElement>(WriteException);
        }
        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => new(
            DataSourceKey: "matter.home",
            DataSourceName: "Matter Home",
            DriverType: "host.sidecar",
            RuntimeInstanceId: "test",
            Endpoint: null,
            State: CommunicationDriverOperationalState.Healthy,
            StateChangedAt: DateTimeOffset.UtcNow,
            CapturedAt: DateTimeOffset.UtcNow,
            LastSuccessfulCommunicationAt: null,
            LastFailedCommunicationAt: null,
            LastError: null,
            DataAge: null,
            ConfiguredScanInterval: null,
            LastOperationDuration: null,
            AverageOperationDuration: null,
            LastScanDuration: null,
            RecentFailureRate: 0,
            AssociatedTagCount: 0,
            TagQuality: new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
            Counters: new CommunicationDriverCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
