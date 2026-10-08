using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Drivers.Zigbee2Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class Zigbee2MqttBridgeTests
{
    private const string Ieee = "0x00124b0024cafe01";

    [Fact]
    public void Composition_RegistersTheDedicatedDriverAndEngineeringDescriptor()
    {
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();

        Assert.True(components.TryGet(Zigbee2MqttContract.DriverType, out var registration));
        Assert.NotNull(registration);
        Assert.IsType<Zigbee2MqttCommunicationRuntimePlanner>(registration!.Planner);
        Assert.Equal(Zigbee2MqttContract.DriverType, registration.Factory.DriverType);
        Assert.Equal(Zigbee2MqttContract.DriverType, registration.Descriptor.DriverType);
    }

    [Fact]
    public void Inventory_MapsOnlyCuratedStateExposes_AndKeepsIdentityAcrossFriendlyNameChanges()
    {
        var dataSourceId = Guid.NewGuid();
        var settings = Settings(dataSourceId);
        var firstInventory = ParseInventory(settings, "lamp-main");
        var secondInventory = ParseInventory(settings, "renamed-lamp");
        var first = Assert.Single(firstInventory.Devices);
        var renamed = Assert.Single(secondInventory.Devices);

        Assert.Equal(2, first.Exposes.Count);
        Assert.Contains(first.Issues!, issue => issue.Code == "Z2M_TRANSIENT_EVENT_EXCLUDED");
        Assert.Contains(first.Issues!, issue => issue.Code == "Z2M_METADATA_EXPOSE_EXCLUDED");
        Assert.Equal(
            Zigbee2MqttIdentity.StableDeviceIdentity(dataSourceId, Ieee),
            Zigbee2MqttIdentity.StableDeviceIdentity(dataSourceId, renamed.IeeeAddress));

        var materialization = Zigbee2MqttExposeMapper.BuildMaterialization(dataSourceId, "home.z2m", first);
        Assert.Equal(2, materialization.Tags!.Count);
        Assert.Equal(2, materialization.Equipment.Capabilities!.Count);
        Assert.Contains(dataSourceId.ToString("N"), materialization.Equipment.StableDeviceIdentity);
        Assert.All(materialization.Tags!, tag => Assert.Contains(dataSourceId.ToString("N"), tag.Path));
        Assert.Contains(materialization.Tags!, tag => tag.PortableAddress == Zigbee2MqttIdentity.PortableAddress(Ieee, null, "state") && !tag.ReadOnly);
        Assert.Contains(materialization.Tags!, tag => tag.PortableAddress == Zigbee2MqttIdentity.PortableAddress(Ieee, null, "temperature") && tag.ReadOnly);

        var stateExpose = Assert.Single(first.Exposes, expose => expose.Property == "state");
        var multiEndpointDevice = first with
        {
            Exposes = [stateExpose with { Endpoint = "left" }, stateExpose with { Endpoint = "right" }]
        };
        var multiEndpoint = Zigbee2MqttExposeMapper.BuildMaterialization(dataSourceId, "home.z2m", multiEndpointDevice);
        Assert.Equal(2, multiEndpoint.Equipment.Capabilities!.Select(capability => capability.CapabilityId).Distinct().Count());
    }

    [Fact]
    public void Mapper_EnforcesExplicitBooleanValuesAndNumericRangeAndStep()
    {
        var settings = Settings(Guid.NewGuid());
        var device = Assert.Single(ParseInventory(settings, "lamp").Devices);
        var binary = Assert.Single(device.Exposes, expose => expose.Property == "state");
        var numeric = Assert.Single(device.Exposes, expose => expose.Property == "temperature");
        var binaryPoint = Point(binary, TagDataType.Boolean, readOnly: false);
        var numericPoint = Point(numeric, TagDataType.Double, readOnly: true);

        using var on = JsonDocument.Parse("{\"state\":\"ON\"}");
        using var off = JsonDocument.Parse("{\"state\":\"OFF\"}");
        using var badBinary = JsonDocument.Parse("{\"state\":\"TOGGLE\"}");
        Assert.True(Zigbee2MqttExposeMapper.TryDecodeValue(binaryPoint, on.RootElement, out var onValue, out _));
        Assert.True(Assert.IsType<bool>(onValue));
        Assert.True(Zigbee2MqttExposeMapper.TryDecodeValue(binaryPoint, off.RootElement, out var offValue, out _));
        Assert.False(Assert.IsType<bool>(offValue));
        Assert.False(Zigbee2MqttExposeMapper.TryDecodeValue(binaryPoint, badBinary.RootElement, out _, out _));
        Assert.Equal("\"ON\"", Zigbee2MqttExposeMapper.EncodeSetValue(binaryPoint, true));

        using var goodNumber = JsonDocument.Parse("{\"temperature\":21.5}");
        using var wrongStep = JsonDocument.Parse("{\"temperature\":21.25}");
        using var outOfRange = JsonDocument.Parse("{\"temperature\":91}");
        Assert.True(Zigbee2MqttExposeMapper.TryDecodeValue(numericPoint, goodNumber.RootElement, out var numericValue, out _));
        Assert.Equal(21.5d, Assert.IsType<double>(numericValue));
        Assert.False(Zigbee2MqttExposeMapper.TryDecodeValue(numericPoint, wrongStep.RootElement, out _, out _));
        Assert.False(Zigbee2MqttExposeMapper.TryDecodeValue(numericPoint, outOfRange.RootElement, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => Zigbee2MqttExposeMapper.EncodeSetValue(Point(numeric, TagDataType.Double, readOnly: false), 21.25d));
    }

    [Fact]
    public void Binding_ValidatesCanonicalDataSourceTypeUnitAccessAndConfirmationPolicy()
    {
        var dataSourceId = Guid.NewGuid();
        var device = Assert.Single(ParseInventory(Settings(dataSourceId), "lamp").Devices);
        var materialization = Zigbee2MqttExposeMapper.BuildMaterialization(dataSourceId, "home.z2m", device);
        var numberCandidate = Assert.Single(materialization.Tags!, tag => tag.DataType == TagDataType.Double);
        var numericBinding = Binding(numberCandidate);

        var numericPoint = Zigbee2MqttEngineeringProvider.CreatePoint(
            numericBinding,
            TagDataType.Double,
            "°C",
            expectedDataSourceId: dataSourceId);
        Assert.Equal(-10d, numericPoint.Minimum!.Value);
        Assert.Equal(80d, numericPoint.Maximum!.Value);
        Assert.Equal(0.5d, numericPoint.Step!.Value);
        Assert.Throws<ArgumentException>(() => Zigbee2MqttEngineeringProvider.CreatePoint(numericBinding, TagDataType.Boolean, "°C"));
        Assert.Throws<ArgumentException>(() => Zigbee2MqttEngineeringProvider.CreatePoint(numericBinding, TagDataType.Double, "°F"));

        var writableCandidate = Assert.Single(materialization.Tags!, tag => tag.DataType == TagDataType.Boolean);
        var writablePoint = Zigbee2MqttEngineeringProvider.CreatePoint(Binding(writableCandidate), TagDataType.Boolean, null, readOnly: false, expectedDataSourceId: dataSourceId);
        Assert.True(writablePoint.Writable);
        var alteredAccess = writableCandidate.Metadata!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        alteredAccess["z2m.access"] = "0";
        Assert.Throws<ArgumentException>(() => Zigbee2MqttEngineeringProvider.CreatePoint(
            Binding(writableCandidate with { Metadata = alteredAccess }),
            TagDataType.Boolean,
            null,
            readOnly: false,
            expectedDataSourceId: dataSourceId));
    }

    [Fact]
    public void RuntimePlanner_UsesCanonicalMaterializationAndRejectsPlaintextPasswordSettings()
    {
        var dataSourceId = Guid.NewGuid();
        var dataSource = new DataSourceEngineeringDto(
            dataSourceId,
            "home.z2m",
            "External Zigbee2MQTT",
            Zigbee2MqttContract.DriverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "mqtt.example.test",
                ["username"] = "z2m-user",
                ["reconnectMinimumMilliseconds"] = "100",
                ["reconnectMaximumMilliseconds"] = "200"
            },
            SecretReferences: new Dictionary<string, string> { ["password"] = "broker-password" });
        var device = Assert.Single(ParseInventory(Settings(dataSourceId), "lamp").Devices);
        var materialized = Zigbee2MqttExposeMapper.BuildMaterialization(dataSourceId, dataSource.Key, device);
        var state = Assert.Single(materialized.Tags!, candidate => candidate.PortableAddress == Zigbee2MqttIdentity.PortableAddress(Ieee, null, "state"));
        var binding = Binding(state);
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            state.Name,
            state.Path,
            state.DataType,
            Source: dataSource.Key,
            Address: state.PortableAddress,
            ReadOnly: state.ReadOnly,
            Metadata: state.Metadata?.ToDictionary(entry => entry.Key, entry => entry.Value),
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var planner = new Zigbee2MqttCommunicationRuntimePlanner();
        var planned = planner.Plan(package, dataSource);
        Assert.True(planned.CanActivate, string.Join(" | ", planned.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<Zigbee2MqttCommunicationRuntimePlan>(planned.Plan);
        Assert.Equal("broker-password", plan.PasswordSecretReference);
        Assert.True(Assert.Single(plan.Points).Writable);

        var plaintextSource = dataSource with
        {
            Settings = dataSource.Settings!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
        };
        var plaintextSettings = plaintextSource.Settings!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        plaintextSettings["password"] = "never-store-here";
        plaintextSource = plaintextSource with { Settings = plaintextSettings };
        var rejected = planner.Plan(package, plaintextSource);
        Assert.False(rejected.CanActivate);
        Assert.Contains(rejected.Issues, issue => issue.Code == "Z2M_PLAINTEXT_CREDENTIAL_FORBIDDEN");
    }

    [Fact]
    public async Task RuntimeFactory_ResolvesProtectedPasswordAndPassesTlsToTheExistingMqttTransport()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: false);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var plan = new Zigbee2MqttCommunicationRuntimePlan(
            "home.z2m",
            "External Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId, tls: true),
            "z2m-user",
            "broker-password-reference",
            [point]);
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: true);
        var resolver = new StaticProtectedMaterialResolver("protected-password-value");
        var services = new CommunicationDriverRuntimeServices(
            "project-z2m",
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            resolver,
            () => true);
        await using var driver = (Zigbee2MqttDriver)new Zigbee2MqttCommunicationRuntimeFactory(() => transport).Create(plan, services);

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);

        Assert.Equal("z2m-user", transport.CapturedUsername);
        Assert.Equal("protected-password-value", transport.CapturedPassword);
        Assert.True(transport.CapturedTls);
        Assert.Equal("broker-password-reference", resolver.LastRequest!.Reference);
        Assert.Equal(Zigbee2MqttContract.PasswordPurpose, resolver.LastRequest.Purpose);
    }

    [Fact]
    public void Inventory_RejectsDuplicatePhysicalIdentityAndMalformedPayloads()
    {
        var settings = Settings(Guid.NewGuid());
        var duplicate = Encoding.UTF8.GetBytes($"[{DeviceJson("lamp-a")},{DeviceJson("lamp-b")}]");
        Assert.Throws<FormatException>(() => Zigbee2MqttExposeMapper.ParseInventory(duplicate, settings));
        Assert.Throws<FormatException>(() => Zigbee2MqttExposeMapper.ParseInventory("{}"u8.ToArray(), settings));
        Assert.Throws<FormatException>(() => Zigbee2MqttExposeMapper.ParseInventory("[]"u8.ToArray(), Settings(Guid.NewGuid(), maximumDevices: 1, maximumInboundPayloadBytes: 1)));
    }

    [Fact]
    public async Task Engineering_DiscoverySelectsMaterializationAndPointReadRequiresFreshPeerReport()
    {
        var dataSourceId = Guid.NewGuid();
        var context = new DriverEngineeringDataSourceContext(
            "home.z2m",
            "External Zigbee2MQTT",
            Zigbee2MqttContract.DriverType,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "mqtt.example.test",
                ["baseTopic"] = "zigbee2mqtt",
                ["reconnectMinimumMilliseconds"] = "100",
                ["reconnectMaximumMilliseconds"] = "200"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var created = new List<FakeMqttTransport>();
        var provider = new Zigbee2MqttEngineeringProvider(
            "project-z2m",
            context.DataSourceKey,
            dataSourceId,
            new NoProtectedMaterialResolver(),
            () =>
            {
                var transport = new FakeMqttTransport("lamp-main", confirmWrites: true, respondToReads: true);
                created.Add(transport);
                return transport;
            });

        var selectedCandidates = new List<DriverDiscoveryCandidate>();
        await foreach (var candidate in provider.DiscoverAsync(new DriverDiscoveryRequest(
                           context,
                           new Dictionary<string, string> { [Zigbee2MqttEngineeringProvider.SelectedIeeeAddressesParameter] = Ieee })))
            selectedCandidates.Add(candidate);

        var selected = Assert.Single(selectedCandidates);
        Assert.Equal(Zigbee2MqttIdentity.StableDeviceIdentity(dataSourceId, Ieee), selected.StableIdentity);
        Assert.NotNull(selected.Materialization);
        Assert.Equal(2, selected.Materialization!.Tags!.Count);

        var candidateTag = Assert.Single(selected.Materialization.Tags!, item => item.PortableAddress == Zigbee2MqttIdentity.PortableAddress(Ieee, null, "state"));
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Zigbee2MqttContract.TagBindingSchemaId,
            Zigbee2MqttContract.TagBindingSchemaVersion,
            candidateTag.PortableAddress!,
            candidateTag.Metadata);
        var read = await provider.TestPointReadAsync(new DriverPointReadTestRequest(
            context,
            binding,
            TagDataType.Boolean,
            EngineeringUnit: null,
            TimeoutMilliseconds: 500));

        Assert.Equal(DriverPointReadTestStatus.Good, read.Status);
        Assert.Equal(1, read.Summary.GoodSamples);
        Assert.All(created, transport => Assert.True(transport.IsDisposed));
        Assert.All(created, transport => Assert.DoesNotContain("password", transport.LastPublishedJson ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Driver_StandbyIsInert_ActivationCancellationDoesNotStopRuntime_AndStopRestartCleansUp()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: false);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var first = new FakeMqttTransport("lamp-main", confirmWrites: true);
        var second = new FakeMqttTransport("lamp-main", confirmWrites: true);
        var transports = new Queue<FakeMqttTransport>([first, second]);
        var factoryCalls = 0;
        var ownsEffects = false;
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-main",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => { factoryCalls++; return transports.Dequeue(); },
            _ => ValueTask.FromResult(MqttResolvedCredentials.None),
            () => ownsEffects);

        await driver.StartAsync();
        Assert.Equal(0, factoryCalls);
        Assert.Equal(DriverState.Stopped, driver.Status.State);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, true));
        Assert.Equal(0, first.SetPublishCount);
        Assert.Empty(first.Subscriptions);

        ownsEffects = true;
        using var activationRequest = new CancellationTokenSource();
        await driver.StartAsync(activationRequest.Token);
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        activationRequest.Cancel();
        Assert.True(first.IsConnected);
        Assert.Equal(1, first.ConnectCount);
        Assert.Contains(first.Subscriptions.SelectMany(value => value), item => item.Topic == "zigbee2mqtt/lamp-main/availability");
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var current) && current?.Quality == TagQuality.Stale);
        Assert.True(cache.TryGet(tag.Id, out var initial));
        Assert.Equal(TagQuality.Stale, initial!.Quality);

        await driver.WriteAsync(tag.Id, true);
        Assert.Equal(1, first.SetPublishCount);
        Assert.True(cache.TryGet(tag.Id, out var confirmed));
        Assert.Equal(TagQuality.Good, confirmed!.Quality);
        Assert.True(Assert.IsType<bool>(confirmed.Value));

        await driver.StopAsync();
        Assert.False(first.IsConnected);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(DriverState.Stopped, driver.Status.State);
        Assert.Equal(CommunicationDriverReadinessState.Stopped, driver.GetCommunicationReadiness().State);

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        Assert.True(second.IsConnected);
        Assert.Equal(1, second.ConnectCount);
        await driver.StopAsync();
        Assert.False(second.IsConnected);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task Driver_AmbiguousWriteIsNotRetriedAndLeavesCachedValueUncertain()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: false);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: false);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-ambiguous",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId, writeConfirmationMilliseconds: 150),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var initial) && initial?.Quality == TagQuality.Stale);

        var ambiguous = driver.WriteAsync(tag.Id, true).AsTask();
        await EventuallyAsync(() => transport.SetPublishCount == 1);
        Assert.True(cache.TryGet(tag.Id, out var duringPublish));
        Assert.NotEqual(TagQuality.Good, duringPublish!.Quality);
        Assert.False(Assert.IsType<bool>(duringPublish.Value));
        await Assert.ThrowsAsync<TimeoutException>(async () => await ambiguous);

        Assert.Equal(1, transport.SetPublishCount);
        Assert.True(cache.TryGet(tag.Id, out var uncertain));
        Assert.Equal(TagQuality.Uncertain, uncertain!.Quality);
        Assert.Equal("1", driver.GetCommunicationDiagnostics().ProtocolDetails!["ambiguousWrites"]);
        Assert.DoesNotContain("password", driver.GetCommunicationDiagnostics().SanitizedEndpoint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Driver_StaleBindingIsBadConfigurationAndCannotReadOrWriteTheDevice()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: false);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "UnexpectedCapability");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: true, respondToReads: true);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-stale-binding",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.BadConfiguration);

        var current = await driver.ReadAsync(tag.Id);
        Assert.Equal(TagQuality.BadConfiguration, current!.Quality);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, true));
        Assert.Equal(0, transport.GetPublishCount);
        Assert.Equal(0, transport.SetPublishCount);
    }

    [Fact]
    public async Task Driver_ReconnectReconcilesBridgeAndResubscribesWithoutPromotingRetainedCache()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: true);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: false, disconnectAfterInitialSnapshot: true);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-reconnect",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => transport.ConnectCount >= 2 && driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);

        Assert.True(transport.Subscriptions.Count >= 4);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Stale);
        Assert.True(cache.TryGet(tag.Id, out var current));
        Assert.Equal(TagQuality.Stale, current!.Quality);
        Assert.Equal(1, driver.GetCommunicationDiagnostics().Counters.Reconnects);
    }

    [Fact]
    public async Task Driver_NonRetainedDuplicateNeedsFreshDeviceEvidenceBeforeGoodQuality()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: true);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: false);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-freshness",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Stale);

        var cachedReport = JsonSerializer.Serialize(new
        {
            state = "OFF",
            last_seen = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture)
        });
        transport.EmitState(cachedReport, retained: false);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Uncertain);
        Assert.True(cache.TryGet(tag.Id, out var retainedOnly));
        Assert.Equal(TagQuality.Uncertain, retainedOnly!.Quality);

        transport.EmitAvailability("online", retained: false);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Stale);
        transport.EmitState("{\"state\":\"OFF\"}", retained: false);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Uncertain);
        Assert.True(cache.TryGet(tag.Id, out var afterAvailability));
        Assert.Equal(TagQuality.Uncertain, afterAvailability!.Quality);

        var freshReport = JsonSerializer.Serialize(new
        {
            state = "OFF",
            last_seen = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });
        transport.EmitState(freshReport, retained: false);
        await EventuallyAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good);
    }

    [Fact]
    public async Task Driver_MalformedDeviceStateMarksBadDeviceAndCompletesPointRead()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: true);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: false);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new Zigbee2MqttDriver(
            "z2m-malformed-state",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            cache,
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);
        var read = driver.ReadAsync(tag.Id).AsTask();
        await EventuallyAsync(() => transport.GetPublishCount == 1);
        transport.EmitState("{not-json", retained: false);

        var result = await read.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(TagQuality.BadDevice, result!.Quality);
        Assert.Equal(CommunicationDriverReadinessState.Ready, driver.GetCommunicationReadiness().State);
        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task Driver_DisposeAttemptsTransportCleanupAndSurfacesStopFailure()
    {
        var dataSourceId = Guid.NewGuid();
        var tag = Tag("Zigbee2Mqtt/Lamp/State", TagDataType.Boolean, readOnly: true);
        var point = new Zigbee2MqttPoint(tag, Ieee, null, "state", Zigbee2MqttValueKind.Boolean, 7, null, null, null, null, "\"ON\"", "\"OFF\"", "OnOff");
        var transport = new FakeMqttTransport("lamp-main", confirmWrites: false, failDisconnect: true);
        var driver = new Zigbee2MqttDriver(
            "z2m-cleanup-failure",
            "Zigbee2MQTT",
            dataSourceId,
            Settings(dataSourceId),
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            [point],
            () => transport,
            _ => ValueTask.FromResult(MqttResolvedCredentials.None));

        await driver.StartAsync();
        await EventuallyAsync(() => driver.GetCommunicationReadiness().State == CommunicationDriverReadinessState.Ready);

        await Assert.ThrowsAsync<IOException>(async () => await driver.DisposeAsync());

        Assert.Equal(1, transport.DisposeCount);
        Assert.False(transport.IsConnected);
        Assert.Equal(DriverState.Faulted, driver.Status.State);
    }

    private static Zigbee2MqttInventory ParseInventory(Zigbee2MqttConnectionSettings settings, string friendlyName) =>
        Zigbee2MqttExposeMapper.ParseInventory(Encoding.UTF8.GetBytes($"[{DeviceJson(friendlyName)}]"), settings, "online");

    private static string DeviceJson(string friendlyName) => $$"""
        {
          "ieee_address": "{{Ieee}}",
          "friendly_name": "{{friendlyName}}",
          "supported": true,
          "model_id": "ZB-01",
          "definition": { "vendor": "Example" },
          "exposes": [
            { "type": "binary", "property": "state", "access": 7, "value_on": "ON", "value_off": "OFF" },
            { "type": "numeric", "property": "temperature", "access": 5, "unit": "°C", "value_min": -10, "value_max": 80, "value_step": 0.5 },
            { "type": "numeric", "property": "linkquality", "access": 1, "category": "diagnostic" },
            { "type": "action", "property": "action", "access": 1 }
          ]
        }
        """;

    private static Zigbee2MqttConnectionSettings Settings(
        Guid dataSourceId,
        int writeConfirmationMilliseconds = 1000,
        int maximumDevices = 16,
        int maximumInboundPayloadBytes = 1_048_576,
        bool tls = false) =>
        Zigbee2MqttConnectionSettings.Parse(
            "home.z2m",
            dataSourceId,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "mqtt.example.test",
                ["tls"] = tls.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["baseTopic"] = "zigbee2mqtt",
                ["writeConfirmationTimeoutMilliseconds"] = writeConfirmationMilliseconds.ToString(CultureInfo.InvariantCulture),
                ["maximumDevices"] = maximumDevices.ToString(CultureInfo.InvariantCulture),
                ["maximumInboundPayloadBytes"] = maximumInboundPayloadBytes.ToString(CultureInfo.InvariantCulture),
                ["reconnectMinimumMilliseconds"] = "100",
                ["reconnectMaximumMilliseconds"] = "200"
            });

    private static TagDefinition Tag(string path, TagDataType type, bool readOnly, string? unit = null) =>
        TagDefinition.Create("Z2M", path, type, engineeringUnit: unit, readOnly: readOnly);

    private static Zigbee2MqttPoint Point(Zigbee2MqttExpose expose, TagDataType type, bool readOnly)
    {
        var tag = Tag($"Zigbee2Mqtt/{Ieee}/{expose.Property}", type, readOnly, expose.Unit);
        return new Zigbee2MqttPoint(
            tag,
            Ieee,
            expose.Endpoint,
            expose.Property,
            expose.ValueKind,
            expose.Access,
            expose.Unit,
            expose.Minimum,
            expose.Maximum,
            expose.Step,
            expose.ValueOnJson,
            expose.ValueOffJson,
            expose.CapabilityKind);
    }

    private static CommunicationTagBinding Binding(DriverMaterializationTagCandidate candidate) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            Zigbee2MqttContract.TagBindingSchemaId,
            Zigbee2MqttContract.TagBindingSchemaVersion,
            candidate.PortableAddress!,
            candidate.Metadata);

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) throw new TimeoutException("The Zigbee2MQTT test condition was not reached.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeMqttTransport(
        string friendlyName,
        bool confirmWrites,
        bool respondToReads = false,
        bool disconnectAfterInitialSnapshot = false,
        bool failDisconnect = false) : IMqttClientTransport
    {
        private readonly Channel<MqttTransportMessage> _messages = Channel.CreateUnbounded<MqttTransportMessage>();
        private int _receivedMessages;
        private bool _didDisconnectOnce;

        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int SetPublishCount { get; private set; }
        public int GetPublishCount { get; private set; }
        public bool IsDisposed => DisposeCount > 0;
        public string? CapturedUsername { get; private set; }
        public string? CapturedPassword { get; private set; }
        public bool CapturedTls { get; private set; }
        public string? LastPublishedJson { get; private set; }
        public List<IReadOnlyCollection<MqttSubscription>> Subscriptions { get; } = [];

        public void EmitState(string payload, bool retained) => Enqueue($"zigbee2mqtt/{friendlyName}", payload, retained);

        public void EmitAvailability(string payload, bool retained) => Enqueue($"zigbee2mqtt/{friendlyName}/availability", payload, retained);

        public ValueTask ConnectAsync(MqttConnectionSettings settings, MqttResolvedCredentials credentials, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedTls = settings.UseTls;
            CapturedUsername = credentials.Username;
            CapturedPassword = credentials.Password.IsEmpty
                ? null
                : Encoding.UTF8.GetString(credentials.Password.Span);
            ConnectCount++;
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SubscribeAsync(IReadOnlyCollection<MqttSubscription> subscriptions, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Subscriptions.Add(subscriptions.ToArray());
            if (subscriptions.Any(item => item.Topic == "zigbee2mqtt/bridge/state"))
            {
                Enqueue("zigbee2mqtt/bridge/state", "online", retained: true);
                Enqueue("zigbee2mqtt/bridge/devices", $"[{Zigbee2MqttBridgeTests.DeviceJson(friendlyName)}]", retained: true);
            }
            if (subscriptions.Any(item => item.Topic == $"zigbee2mqtt/{friendlyName}"))
            {
                Enqueue($"zigbee2mqtt/{friendlyName}/availability", "online", retained: true);
                Enqueue($"zigbee2mqtt/{friendlyName}", "{\"state\":\"OFF\"}", retained: true);
            }
            return ValueTask.CompletedTask;
        }

        public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (disconnectAfterInitialSnapshot && !_didDisconnectOnce && _receivedMessages >= 4)
            {
                _didDisconnectOnce = true;
                throw new IOException("Simulated broker disconnect after retained startup snapshot.");
            }
            var message = await _messages.Reader.ReadAsync(cancellationToken);
            _receivedMessages++;
            return message;
        }

        public ValueTask PublishAsync(MqttPublishRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPublishedJson = Encoding.UTF8.GetString(request.Payload.Span);
            if (request.Topic == $"zigbee2mqtt/{friendlyName}/set")
            {
                SetPublishCount++;
                if (confirmWrites)
                {
                    using var document = JsonDocument.Parse(request.Payload);
                    var value = document.RootElement.GetProperty("state").GetString();
                    var state = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["state"] = value,
                        ["last_seen"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    });
                    Enqueue($"zigbee2mqtt/{friendlyName}", state, retained: false);
                }
            }
            else if (request.Topic == $"zigbee2mqtt/{friendlyName}/get")
            {
                GetPublishCount++;
                if (respondToReads)
                {
                    var state = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["state"] = "OFF",
                        ["last_seen"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    });
                    Enqueue($"zigbee2mqtt/{friendlyName}", state, retained: false);
                }
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            if (failDisconnect) throw new IOException("Simulated disconnect cleanup failure.");
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        private void Enqueue(string topic, string payload, bool retained) =>
            _messages.Writer.TryWrite(new MqttTransportMessage(
                topic,
                Encoding.UTF8.GetBytes(payload),
                retained,
                MqttQosLevel.AtLeastOnce,
                DateTimeOffset.UtcNow));
    }

    private sealed class NoProtectedMaterialResolver : ICommunicationDriverProtectedMaterialResolver
    {
        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("This test does not configure protected MQTT credentials.");
    }

    private sealed class StaticProtectedMaterialResolver(string password) : ICommunicationDriverProtectedMaterialResolver
    {
        public CommunicationDriverProtectedMaterialRequest? LastRequest { get; private set; }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(new StaticProtectedMaterialLease(password));
        }
    }

    private sealed class StaticProtectedMaterialLease(string password) : ICommunicationDriverProtectedMaterialLease
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(password);
        public ReadOnlyMemory<byte> Material => _bytes;
        public string? ContentType => "text/plain";
        public ValueTask DisposeAsync()
        {
            Array.Clear(_bytes, 0, _bytes.Length);
            return ValueTask.CompletedTask;
        }
    }
}
