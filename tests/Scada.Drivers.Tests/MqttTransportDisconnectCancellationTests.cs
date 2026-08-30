using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttTransportDisconnectCancellationTests
{
    [Fact]
    public async Task AcceptedDisconnectCompletesAfterCallerTokenIsCanceled()
    {
        var client = new BlockingDisconnectMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        using var callerCancellation = new CancellationTokenSource();
        var disconnect = transport.DisconnectAsync(callerCancellation.Token).AsTask();

        await client.DisconnectEntered.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(client.AcceptedDisconnectToken.CanBeCanceled);

        callerCancellation.Cancel();
        Assert.False(disconnect.IsCompleted);

        client.ReleaseDisconnect();
        await disconnect;

        Assert.False(transport.IsConnected);

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        Assert.True(transport.IsConnected);
    }

    [Fact]
    public async Task CancellationBeforeLifecycleAdmissionLeavesConnectedSessionUntouched()
    {
        var client = new BlockingDisconnectMqttClient();
        await using var transport = new MqttNetClientTransport(new MqttClientFactory(), client);
        var settings = CreateSettings();

        using (var credentials = MqttResolvedCredentials.None)
            await transport.ConnectAsync(settings, credentials);

        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.DisconnectAsync(callerCancellation.Token).AsTask());

        Assert.True(transport.IsConnected);
        Assert.False(client.DisconnectEntered.IsCompleted);
    }

    private static MqttConnectionSettings CreateSettings() => new(
        "broker.local",
        1883,
        UseTls: false,
        ClientId: "elite-disconnect-cancellation",
        MaximumBufferedMessages: 4);

    private sealed class BlockingDisconnectMqttClient : IMqttClient
    {
        private readonly TaskCompletionSource<bool> _disconnectEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _disconnectRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private MqttClientOptions? _options;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync
        {
            add { }
            remove { }
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

        public Task DisconnectEntered => _disconnectEntered.Task;

        public CancellationToken AcceptedDisconnectToken { get; private set; }

        public void ReleaseDisconnect() => _disconnectRelease.TrySetResult(true);

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

        public async Task DisconnectAsync(
            MqttClientDisconnectOptions options,
            CancellationToken cancellationToken = default)
        {
            AcceptedDisconnectToken = cancellationToken;
            _disconnectEntered.TrySetResult(true);
            await _disconnectRelease.Task.WaitAsync(cancellationToken);
            IsConnected = false;
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
            _disconnectRelease.TrySetResult(true);
        }
    }
}
