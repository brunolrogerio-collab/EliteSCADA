using System.Text;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttConnectBacklogAdmissionTests
{
    [Theory]
    [InlineData(MqttQualityOfServiceLevel.AtMostOnce, 0)]
    [InlineData(MqttQualityOfServiceLevel.AtLeastOnce, 1)]
    public async Task PersistentBacklogDeliveredBeforeConnectReturnsIsAdmitted(
        MqttQualityOfServiceLevel qos,
        int expectedAcknowledgements)
    {
        var client = new BacklogDuringConnectMqttClient(qos);
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = new MqttConnectionSettings(
            "broker.local",
            1883,
            UseTls: false,
            ClientId: "elite-connect-backlog",
            MaximumBufferedMessages: 4);

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await transport.ReceiveAsync(timeout.Token);

        Assert.NotNull(client.DeliveredArgs);
        Assert.False(client.DeliveredArgs!.ProcessingFailed);
        Assert.Equal(expectedAcknowledgements, client.AcknowledgementCount);
        Assert.Equal("plant/persistent/backlog", received.Topic);
        Assert.Equal("backlog-before-connect-return", Encoding.UTF8.GetString(received.Payload.Span));
    }

    private sealed class BacklogDuringConnectMqttClient : IMqttClient
    {
        private readonly MqttQualityOfServiceLevel _qos;
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _applicationMessageReceivedAsync;
        private MqttClientOptions? _options;
        private int _acknowledgementCount;

        public BacklogDuringConnectMqttClient(MqttQualityOfServiceLevel qos)
        {
            _qos = qos;
        }

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
            add { }
            remove { }
        }

        public event Func<InspectMqttPacketEventArgs, Task> InspectPacketAsync
        {
            add { }
            remove { }
        }

        public bool IsConnected { get; private set; }

        public MqttClientOptions Options =>
            _options ?? throw new InvalidOperationException("Client has not connected yet.");

        public MqttApplicationMessageReceivedEventArgs? DeliveredArgs { get; private set; }

        public int AcknowledgementCount => Volatile.Read(ref _acknowledgementCount);

        public async Task<MqttClientConnectResult> ConnectAsync(
            MqttClientOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _options = options;
            IsConnected = true;

            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("plant/persistent/backlog")
                .WithPayload("backlog-before-connect-return")
                .WithQualityOfServiceLevel(_qos)
                .Build();
            var publishPacket = new MqttPublishPacket
            {
                Topic = applicationMessage.Topic,
                Payload = applicationMessage.Payload,
                QualityOfServiceLevel = applicationMessage.QualityOfServiceLevel,
                Retain = applicationMessage.Retain
            };
            var args = new MqttApplicationMessageReceivedEventArgs(
                options.ClientId,
                applicationMessage,
                publishPacket,
                (_, acknowledgeCancellation) =>
                {
                    acknowledgeCancellation.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref _acknowledgementCount);
                    return Task.CompletedTask;
                });

            DeliveredArgs = args;
            if (_applicationMessageReceivedAsync is not null)
                await _applicationMessageReceivedAsync(args);

            return new MqttClientConnectResult
            {
                ResultCode = MqttClientConnectResultCode.Success
            };
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
        }
    }
}
