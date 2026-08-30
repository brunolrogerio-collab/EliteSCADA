using System.Text;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttTransportSessionQueueTests
{
    [Fact]
    public async Task ReceiveCapturedFromEndedSessionCannotConsumeReconnectedMessage()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var oldReceive = transport.ReceiveAsync().AsTask();
        Assert.False(oldReceive.IsCompleted);

        await transport.DisconnectAsync();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var currentHandler = Assert.Single(client.ApplicationMessageHandlers);
        var currentArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "current-session",
            () => client.RecordAcknowledgement());
        await currentHandler(currentArgs);

        var oldException = await Assert.ThrowsAsync<MqttTransportException>(() => oldReceive);
        Assert.Contains("session ended", oldException.Message, StringComparison.OrdinalIgnoreCase);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var current = await transport.ReceiveAsync(timeout.Token);

        Assert.Equal("current-session", Encoding.UTF8.GetString(current.Payload.Span));
        Assert.Equal(1, client.AcknowledgementCount);
        Assert.False(currentArgs.ProcessingFailed);
    }

    [Fact]
    public async Task ExplicitDisconnectDropsUnreadItemsBeforeAnotherSessionStarts()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var firstHandler = Assert.Single(client.ApplicationMessageHandlers);
        var oldArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "old-unread",
            () => client.RecordAcknowledgement());
        await firstHandler(oldArgs);
        Assert.Equal(1, client.AcknowledgementCount);

        await transport.DisconnectAsync();

        var receiveAfterDisconnect = transport.ReceiveAsync().AsTask();
        var exception = await Assert.ThrowsAsync<MqttTransportException>(
            () => receiveAfterDisconnect.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Contains("connect before receiving", exception.Message, StringComparison.OrdinalIgnoreCase);

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var currentHandler = Assert.Single(client.ApplicationMessageHandlers);
        var currentArgs = CreateApplicationMessageArgs(
            settings.ClientId,
            "new-current",
            () => client.RecordAcknowledgement());
        await currentHandler(currentArgs);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var current = await transport.ReceiveAsync(timeout.Token);

        Assert.Equal("new-current", Encoding.UTF8.GetString(current.Payload.Span));
        Assert.Equal(2, client.AcknowledgementCount);
        Assert.False(currentArgs.ProcessingFailed);
    }

    private static MqttConnectionSettings CreateSettings() => new(
        "broker.local",
        1883,
        UseTls: false,
        ClientId: "elite-session-queue",
        MaximumBufferedMessages: 4);

    private static MqttApplicationMessageReceivedEventArgs CreateApplicationMessageArgs(
        string clientId,
        string payload,
        Action acknowledged)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic("plant/session-queue/value")
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
        private int _acknowledgementCount;

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

        public int AcknowledgementCount => Volatile.Read(ref _acknowledgementCount);

        public void RecordAcknowledgement() => Interlocked.Increment(ref _acknowledgementCount);

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
