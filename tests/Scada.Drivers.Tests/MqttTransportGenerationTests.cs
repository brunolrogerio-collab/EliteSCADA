using System.Runtime.CompilerServices;
using System.Text;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttTransportGenerationTests
{
    [Fact]
    public async Task StaleApplicationCallbackFromPriorSessionCannotEnterReconnectedQueue()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var staleHandler = Assert.Single(client.ApplicationMessageHandlers);

        await transport.DisconnectAsync();
        Assert.Empty(client.ApplicationMessageHandlers);

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var currentHandler = Assert.Single(client.ApplicationMessageHandlers);
        var staleAcknowledgements = 0;
        var currentAcknowledgements = 0;
        var staleArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "stale",
            () => Interlocked.Increment(ref staleAcknowledgements));
        var currentArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "current",
            () => Interlocked.Increment(ref currentAcknowledgements));

        // This delegate was captured from the first transport session. Even though
        // it executes after the second session is accepting events, its generation
        // must remain the old one and therefore fail closed without ACK/admission.
        await staleHandler(staleArgs);
        await currentHandler(currentArgs);

        var received = await transport.ReceiveAsync();

        Assert.True(staleArgs.ProcessingFailed);
        Assert.Equal(0, staleAcknowledgements);
        Assert.False(currentArgs.ProcessingFailed);
        Assert.Equal(1, currentAcknowledgements);
        Assert.Equal("current", Encoding.UTF8.GetString(received.Payload.Span));
    }

    [Fact]
    public async Task StaleDisconnectCallbackFromPriorSessionCannotPoisonReconnectedQueue()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var staleDisconnectHandler = Assert.Single(client.DisconnectedHandlers);

        await transport.DisconnectAsync();
        Assert.Empty(client.DisconnectedHandlers);

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var currentMessageHandler = Assert.Single(client.ApplicationMessageHandlers);
        var currentAcknowledgements = 0;
        var currentArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "current-after-stale-disconnect",
            () => Interlocked.Increment(ref currentAcknowledgements));

        // Only session identity matters here. A stale generation must return before
        // any disconnect details are inspected or an error is admitted to the queue.
        var staleDisconnectArgs = (MqttClientDisconnectedEventArgs)RuntimeHelpers.GetUninitializedObject(
            typeof(MqttClientDisconnectedEventArgs));
        await staleDisconnectHandler(staleDisconnectArgs);
        await currentMessageHandler(currentArgs);

        var received = await transport.ReceiveAsync();

        Assert.True(transport.IsConnected);
        Assert.Equal(1, currentAcknowledgements);
        Assert.Equal("current-after-stale-disconnect", Encoding.UTF8.GetString(received.Payload.Span));
    }

    private static MqttConnectionSettings CreateSettings() => new(
        "broker.local",
        1883,
        UseTls: false,
        ClientId: "elite-session-generation",
        MaximumBufferedMessages: 4);

    private static MqttApplicationMessageReceivedEventArgs CreateApplicationMessageArgs(
        string clientId,
        string payload,
        Action acknowledged)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic("plant/session-generation/value")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        var publishPacket = new MqttPublishPacket
        {
            Topic = applicationMessage.Topic,
            Payload = applicationMessage.Payload,
            QualityOfServiceLevel = applicationMessage.QualityOfServiceLevel,
            Retain = applicationMessage.Retain
        };

        return new MqttApplicationMessageReceivedEventArgs(
            clientId,
            applicationMessage,
            publishPacket,
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                acknowledged();
                return Task.CompletedTask;
            });
    }

    private sealed class FakeMqttClient : IMqttClient
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _applicationMessageReceivedAsync;
        private Func<MqttClientDisconnectedEventArgs, Task>? _disconnectedAsync;
        private MqttClientOptions? _options;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync
        {
            add => _applicationMessageReceivedAsync += value;
            remove => _applicationMessageReceivedAsync -= value;
        }

        public event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync
        {
            add { }
            remove { }
        }

        public event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync
        {
            add { }
            remove { }
        }

        public event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync
        {
            add => _disconnectedAsync += value;
            remove => _disconnectedAsync -= value;
        }

        public event Func<InspectMqttPacketEventArgs, Task> InspectPacketAsync
        {
            add { }
            remove { }
        }

        public bool IsConnected { get; private set; }

        public MqttClientOptions Options => _options ?? throw new InvalidOperationException("Client has not connected yet.");

        public IReadOnlyList<Func<MqttApplicationMessageReceivedEventArgs, Task>> ApplicationMessageHandlers =>
            _applicationMessageReceivedAsync?.GetInvocationList()
                .Cast<Func<MqttApplicationMessageReceivedEventArgs, Task>>()
                .ToArray() ?? [];

        public IReadOnlyList<Func<MqttClientDisconnectedEventArgs, Task>> DisconnectedHandlers =>
            _disconnectedAsync?.GetInvocationList()
                .Cast<Func<MqttClientDisconnectedEventArgs, Task>>()
                .ToArray() ?? [];

        public Task<MqttClientConnectResult> ConnectAsync(
            MqttClientOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _options = options;
            IsConnected = true;
            return Task.FromResult(new MqttClientConnectResult
            {
                ResultCode = MqttClientConnectResultCode.Success
            });
        }

        public Task DisconnectAsync(
            MqttClientDisconnectOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task PingAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MqttClientPublishResult> PublishAsync(
            MqttApplicationMessage applicationMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SendEnhancedAuthenticationExchangeDataAsync(
            MqttEnhancedAuthenticationExchangeData data,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MqttClientSubscribeResult> SubscribeAsync(
            MqttClientSubscribeOptions options,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(
            MqttClientUnsubscribeOptions options,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            IsConnected = false;
            _applicationMessageReceivedAsync = null;
            _disconnectedAsync = null;
        }
    }
}
