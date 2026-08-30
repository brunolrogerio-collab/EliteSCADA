using System.Text;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttPayloadByteBudgetTests
{
    [Fact]
    public async Task SecondPayloadWaitsForQueuedByteBudgetUntilFirstMessageIsConsumed()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(
            new MqttClientFactory(),
            client,
            maximumBufferedPayloadBytes: 4);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var handler = Assert.Single(client.ApplicationMessageHandlers);
        var first = CreateApplicationMessageArgs(settings.ClientId, "abc", client.RecordAcknowledgement);
        var second = CreateApplicationMessageArgs(settings.ClientId, "def", client.RecordAcknowledgement);

        await handler(first);
        Assert.Equal(1, client.AcknowledgementCount);

        var secondAdmission = handler(second);
        await Task.Delay(40);
        Assert.False(secondAdmission.IsCompleted);
        Assert.Equal(1, client.AcknowledgementCount);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var firstReceived = await transport.ReceiveAsync(timeout.Token);
        Assert.Equal("abc", Encoding.UTF8.GetString(firstReceived.Payload.Span));

        await secondAdmission.WaitAsync(timeout.Token);
        Assert.Equal(2, client.AcknowledgementCount);
        Assert.False(second.ProcessingFailed);

        var secondReceived = await transport.ReceiveAsync(timeout.Token);
        Assert.Equal("def", Encoding.UTF8.GetString(secondReceived.Payload.Span));
    }

    [Fact]
    public async Task DisconnectCancelsPayloadWaitingForByteBudgetWithoutAcknowledgingIt()
    {
        var client = new FakeMqttClient();
        await using var transport = new MqttNetClientTransport(
            new MqttClientFactory(),
            client,
            maximumBufferedPayloadBytes: 4);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        var handler = Assert.Single(client.ApplicationMessageHandlers);
        var first = CreateApplicationMessageArgs(settings.ClientId, "abc", client.RecordAcknowledgement);
        var blocked = CreateApplicationMessageArgs(settings.ClientId, "def", client.RecordAcknowledgement);

        await handler(first);
        var blockedAdmission = handler(blocked);
        await Task.Delay(40);
        Assert.False(blockedAdmission.IsCompleted);

        await transport.DisconnectAsync();
        await blockedAdmission.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, client.AcknowledgementCount);
        Assert.True(blocked.ProcessingFailed);
        await Assert.ThrowsAsync<MqttTransportException>(() => transport.ReceiveAsync().AsTask());
    }

    [Fact]
    public void ProductionBudgetAllowsMaximumPolicyCompliantPayload()
    {
        Assert.Equal(
            MqttConnectionSettings.MaximumAllowedInboundPayloadBytes,
            MqttPayloadByteBudget.DefaultCapacityBytes);
    }

    private static MqttConnectionSettings CreateSettings() => new(
        "broker.local",
        1883,
        UseTls: false,
        ClientId: "elite-byte-budget",
        MaximumInboundPayloadBytes: 4,
        MaximumBufferedMessages: 4);

    private static MqttApplicationMessageReceivedEventArgs CreateApplicationMessageArgs(
        string clientId,
        string payload,
        Action acknowledged)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic("plant/byte-budget/value")
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
