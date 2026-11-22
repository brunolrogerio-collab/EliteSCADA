using System.Text;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MqttRuntimeActivationConvergenceTests
{
    [Fact]
    public async Task Coordinator_ActivatesMqttThroughSharedComponentsWithoutWaitingForFirstSample()
    {
        var transport = new CaptureTransport();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(() => transport);
        var compiler = new EngineeringDriverCompiler(components);
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        var firstSample = new TaskCompletionSource<TagValueChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sampleSubscription = eventBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == tagId)
                firstSample.TrySetResult(evt);
            return ValueTask.CompletedTask;
        });
        await using var coordinator = new EngineeringRuntimeCoordinator(
            eventBus,
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var dataSource = new DataSourceEngineeringDto(
            Guid.NewGuid(),
            "mqtt.runtime",
            "Runtime MQTT",
            MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.example.internal",
                ["port"] = "1883",
                ["tls"] = "false",
                ["clientId"] = "elite-runtime-convergence",
                ["protocolVersion"] = "mqtt5"
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MqttDriverDescriptorProvider.SchemaId,
            1,
            "plant/runtime/value",
            new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "/value",
                ["mqtt.sourceTimestampJsonPointer"] = "/timestamp",
                ["mqtt.sourceTimestampRequired"] = "true"
            });
        var tag = new TagEngineeringDto(
            tagId,
            "RuntimeValue",
            "Plant.Runtime.Value",
            TagDataType.Double,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: true,
            CommunicationBinding: binding);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var compilation = compiler.Compile(package);
        Assert.True(compilation.CanActivate);
        Assert.Empty(compilation.ModbusTcpPlans);
        var communicationPlan = Assert.Single(compilation.CommunicationPlans);
        Assert.IsType<MqttCommunicationRuntimePlan>(communicationPlan);

        var result = await coordinator.ActivateAsync("project-a", 1, package);

        Assert.True(result.Activated, string.Join(" | ", result.CompilationIssues.Select(x => x.Message)
            .Concat(result.RuntimeIssues.Select(x => x.Message))));
        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.SubscribeCount);
        Assert.Contains(coordinator.Tags(), active => active.Id == tagId && active.CommunicationBinding == binding);
        Assert.False(coordinator.TryGetCurrent(tagId, out _));
        var driver = Assert.Single(coordinator.Describe().Drivers);
        Assert.Equal(dataSource.Name, driver.Name);

        var receivedAt = DateTimeOffset.Parse("2026-10-15T12:00:00.123456Z");
        var sourceAt = DateTimeOffset.Parse("2026-10-15T11:59:59-03:00");
        transport.Send(new MqttTransportMessage(
            "plant/runtime/value",
            Encoding.UTF8.GetBytes("{\"value\":12.5,\"timestamp\":\"2026-10-15T11:59:59-03:00\"}"),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            receivedAt));
        var published = await firstSample.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(receivedAt, published.Current.Timestamp);
        Assert.Equal(sourceAt.ToUniversalTime(), published.Current.SourceTimestamp);
        Assert.Equal(TimestampEvidenceState.PresentUnverified, published.Current.SourceTimestampEvidenceState);
        Assert.Equal(TimestampEvidenceState.NotProvided, published.Current.ServerTimestampEvidenceState);
    }

    [Fact]
    public async Task Coordinator_UsesHostComposedProtectedMaterialForMqttPasswordReference()
    {
        const string projectKey = "project-a";
        const string dataSourceKey = "mqtt.runtime.secure";
        const string username = "runtime-user";
        const string passwordReference = "password-primary";
        const string passwordMaterial = "scope-secret";

        var protectedMaterialRequest = new CommunicationDriverProtectedMaterialRequest(
            projectKey,
            dataSourceKey,
            MqttDriverDescriptorProvider.DriverType,
            "mqtt.password",
            passwordReference);
        var expectedEnvironmentVariable =
            EnvironmentCommunicationDriverProtectedMaterialResolver.GetDeterministicEnvironmentVariableName(
                protectedMaterialRequest);
        string? requestedEnvironmentVariable = null;
        var hostResolver = EnvironmentCommunicationDriverProtectedMaterialResolver.CreateDeterministicScopedEnvironment(
            name =>
            {
                requestedEnvironmentVariable = name;
                return name == expectedEnvironmentVariable ? passwordMaterial : null;
            });

        var transport = new CaptureTransport();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            () => transport,
            hostResolver);
        var compiler = new EngineeringDriverCompiler(components);
        var eventBus = new InMemoryScadaEventBus();
        await using var coordinator = new EngineeringRuntimeCoordinator(
            eventBus,
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var dataSource = new DataSourceEngineeringDto(
            Guid.NewGuid(),
            dataSourceKey,
            "Secure Runtime MQTT",
            MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.example.internal",
                ["port"] = "1883",
                ["tls"] = "false",
                ["clientId"] = "elite-runtime-secure-convergence",
                ["protocolVersion"] = "mqtt5",
                ["username"] = username
            },
            SecretReferences: new Dictionary<string, string>
            {
                ["password"] = passwordReference
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MqttDriverDescriptorProvider.SchemaId,
            1,
            "plant/runtime/secure-value");
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "SecureRuntimeValue",
            "Plant.Runtime.SecureValue",
            TagDataType.Double,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: true,
            CommunicationBinding: binding);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var compilation = compiler.Compile(package);
        Assert.True(compilation.CanActivate, string.Join(" | ", compilation.Issues.Select(x => x.Message)));
        var plan = Assert.IsType<MqttCommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));
        Assert.Equal(username, plan.Username);
        Assert.Equal(passwordReference, plan.PasswordSecretReference);

        var result = await coordinator.ActivateAsync(projectKey, 2, package);

        Assert.True(result.Activated, string.Join(" | ", result.CompilationIssues.Select(x => x.Message)
            .Concat(result.RuntimeIssues.Select(x => x.Message))));
        Assert.Equal(expectedEnvironmentVariable, requestedEnvironmentVariable);
        Assert.Equal(username, transport.CapturedUsername);
        Assert.Equal(passwordMaterial, transport.CapturedPassword);
        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.SubscribeCount);
    }

    private sealed class CaptureTransport : IMqttClientTransport
    {
        private readonly Channel<MqttTransportMessage> _messages = Channel.CreateUnbounded<MqttTransportMessage>();

        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public string? CapturedUsername { get; private set; }
        public string? CapturedPassword { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = settings;
            CapturedUsername = credentials.Username;
            CapturedPassword = credentials.Password.IsEmpty
                ? null
                : Encoding.UTF8.GetString(credentials.Password.Span);
            ConnectCount++;
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
            return await _messages.Reader.ReadAsync(cancellationToken);
        }

        public void Send(MqttTransportMessage message) => _messages.Writer.TryWrite(message);

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
}
