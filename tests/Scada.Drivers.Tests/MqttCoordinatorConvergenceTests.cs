using System.Security.Cryptography;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MqttCoordinatorConvergenceTests
{
    [Fact]
    public void Planner_UsesV15BindingAsCanonicalTagConfiguration()
    {
        var dataSource = DataSource();
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MqttDriverDescriptorProvider.SchemaId,
            1,
            "plant/tank/state",
            new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "/level",
                ["mqtt.sourceTimestampJsonPointer"] = "/timestamp",
                ["mqtt.sourceTimestampRequired"] = "true",
                ["mqtt.qos"] = "2"
            });
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Level",
            "Plant.Tank.Level",
            TagDataType.Double,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: true,
            Metadata: new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "utf8Scalar",
                ["asset.id"] = "tank-01"
            },
            CommunicationBinding: binding);
        var package = Package([tag], [dataSource]);

        var result = new MqttCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var plan = Assert.IsType<MqttCommunicationRuntimePlan>(result.Plan);
        Assert.Equal(MqttDriverDescriptorProvider.DriverType, plan.DriverType);
        var point = Assert.Single(plan.Points);
        Assert.Equal(MqttPayloadFormat.Json, point.PayloadFormat);
        Assert.Equal("/level", point.JsonPointer);
        Assert.Equal("/timestamp", point.SourceTimestampJsonPointer);
        Assert.True(point.SourceTimestampRequired);
        Assert.Equal(MqttQosLevel.ExactlyOnce, point.Qos);
        Assert.Same(binding, point.Tag.CommunicationBinding);
        Assert.Equal("tank-01", point.Tag.Metadata!["asset.id"]);
        Assert.False(point.Tag.Metadata.ContainsKey("mqtt.payloadFormat"));
    }

    [Fact]
    public void Planner_KeepsLegacyAddressMetadataActivatableWithMigrationWarning()
    {
        var dataSource = DataSource();
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Level",
            "Plant.Tank.LegacyLevel",
            TagDataType.Double,
            Source: dataSource.Key,
            Address: "plant/tank/legacy",
            ReadOnly: true,
            Metadata: new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "/level"
            });

        var result = new MqttCommunicationRuntimePlanner().Plan(Package([tag], [dataSource]), dataSource);

        Assert.True(result.CanActivate);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "MQTT_TAG_LEGACY_BINDING" && !issue.IsError && issue.TagPath == tag.Path);
        var point = Assert.Single(Assert.IsType<MqttCommunicationRuntimePlan>(result.Plan).Points);
        Assert.Equal(MqttPayloadFormat.Json, point.PayloadFormat);
        Assert.Null(point.Tag.CommunicationBinding);
    }

    [Fact]
    public void Planner_FailsClosedOnForeignBindingSchemaAndPhysicalTransform()
    {
        var dataSource = DataSource();
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "foreign.protocol.tag",
            1,
            "plant/tank/state",
            ValueTransform: new TagPhysicalValueTransform(ByteSwap: true));
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Level",
            "Plant.Tank.Invalid",
            TagDataType.Double,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            CommunicationBinding: binding);

        var result = new MqttCommunicationRuntimePlanner().Plan(Package([tag], [dataSource]), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_TAG_BINDING_SCHEMA_MISMATCH");
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_TAG_BINDING_TRANSFORM_UNSUPPORTED");
    }

    [Fact]
    public void Factory_FailsClosedWhenProtectedCredentialHasNoSharedResolver()
    {
        var factory = new MqttCommunicationRuntimeFactory(() => new CaptureTransport());
        var plan = RuntimePlan(passwordSecretReference: "secret://mqtt/operator");
        var services = new CommunicationDriverRuntimeServices(
            "project-a",
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry());

        var error = Assert.Throws<InvalidOperationException>(() => factory.Create(plan, services));

        Assert.Contains("protected-material resolver", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Factory_UsesScopedSharedProtectedMaterialAndSharedReadiness()
    {
        var transport = new CaptureTransport();
        var resolver = new TrackingProtectedMaterialResolver("ephemeral-mqtt-password"u8.ToArray());
        var factory = new MqttCommunicationRuntimeFactory(() => transport);
        var plan = RuntimePlan(
            username: "operator",
            passwordSecretReference: "secret://mqtt/operator");
        var services = new CommunicationDriverRuntimeServices(
            "project-a",
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            resolver);

        await using var driver = factory.Create(plan, services);
        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1 && transport.SubscribeCount == 1);
        var readinessSource = Assert.IsAssignableFrom<ICommunicationDriverReadinessSource>(driver);
        await WaitUntilAsync(() => readinessSource.GetCommunicationReadiness().IsReady);

        Assert.NotNull(resolver.LastRequest);
        Assert.Equal("project-a", resolver.LastRequest!.ProjectKey);
        Assert.Equal("mqtt.plant", resolver.LastRequest.DataSourceKey);
        Assert.Equal(MqttDriverDescriptorProvider.DriverType, resolver.LastRequest.DriverType);
        Assert.Equal("mqtt.password", resolver.LastRequest.Purpose);
        Assert.Equal("secret://mqtt/operator", resolver.LastRequest.Reference);
        Assert.True(resolver.LastLease!.Disposed);
        Assert.Equal("operator", transport.LastUsername);
        Assert.True(transport.LastPasswordLength > 0);

        var readiness = readinessSource.GetCommunicationReadiness();
        Assert.Equal("mqtt.plant", readiness.DataSourceKey);
        Assert.Equal(MqttDriverDescriptorProvider.DriverType, readiness.DriverType);
        Assert.Equal(CommunicationDriverReadinessState.Ready, readiness.State);
        Assert.Equal("true", readiness.Details!["initialHandshakeCompleted"]);
    }

    private static DataSourceEngineeringDto DataSource() =>
        new(
            Guid.NewGuid(),
            "mqtt.plant",
            "Plant MQTT",
            MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.example.internal",
                ["port"] = "1883",
                ["tls"] = "false",
                ["clientId"] = "elite-plant-01",
                ["protocolVersion"] = "mqtt5"
            });

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            dataSources);

    private static MqttCommunicationRuntimePlan RuntimePlan(
        string? username = null,
        string? passwordSecretReference = null)
    {
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MqttDriverDescriptorProvider.SchemaId,
            1,
            "plant/value");
        var tag = TagDefinition.Create(
            "Value",
            $"Plant.Value.{Guid.NewGuid():N}",
            TagDataType.Double,
            source: "mqtt.plant",
            readOnly: true,
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding);

        return new MqttCommunicationRuntimePlan(
            "mqtt.plant",
            "Plant MQTT",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-coordinator-test"),
            username,
            passwordSecretReference,
            [new MqttPoint(tag, binding.PortableAddress)]);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(5);
        }

        Assert.True(predicate(), "Condition did not become true before the test timeout.");
    }

    private sealed class CaptureTransport : IMqttClientTransport
    {
        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public string? LastUsername { get; private set; }
        public int LastPasswordLength { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = settings;
            ConnectCount++;
            LastUsername = credentials.Username;
            LastPasswordLength = credentials.Password.Length;
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SubscribeAsync(
            IReadOnlyCollection<MqttSubscription> subscriptions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEmpty(subscriptions);
            SubscribeCount++;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after cancellation.");
        }

        public ValueTask PublishAsync(
            MqttPublishRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingProtectedMaterialResolver : ICommunicationDriverProtectedMaterialResolver
    {
        private readonly byte[] _material;

        public TrackingProtectedMaterialResolver(byte[] material) => _material = material;

        public CommunicationDriverProtectedMaterialRequest? LastRequest { get; private set; }
        public TrackingLease? LastLease { get; private set; }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            LastRequest = request;
            LastLease = new TrackingLease(_material.ToArray());
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(LastLease);
        }
    }

    private sealed class TrackingLease : ICommunicationDriverProtectedMaterialLease
    {
        private byte[] _material;

        public TrackingLease(byte[] material) => _material = material;

        public bool Disposed { get; private set; }
        public ReadOnlyMemory<byte> Material => Disposed ? ReadOnlyMemory<byte>.Empty : _material;
        public string? ContentType => "application/octet-stream";

        public ValueTask DisposeAsync()
        {
            if (!Disposed && _material.Length > 0)
                CryptographicOperations.ZeroMemory(_material);
            _material = Array.Empty<byte>();
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
