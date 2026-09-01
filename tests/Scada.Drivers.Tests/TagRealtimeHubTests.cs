using System.Net.WebSockets;
using Microsoft.Extensions.Configuration;
using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.ImportExport;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class TagRealtimeHubTests
{
    [Fact]
    public async Task PublishAsync_DoesNotWaitForStalledClientTransport()
    {
        var bus = new InMemoryScadaEventBus();
        using var workspace = new EngineeringWorkspace();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "false"
        };
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
        using var hub = new TagRealtimeHub(bus, security, runtime: null!);
        using var socket = new ControlledWebSocket(blockSends: true);
        using var connectionCancellation = new CancellationTokenSource();
        var connection = hub.HandleAsync(
            socket,
            new SecurityPrincipal("slow-client", null, Array.Empty<string>()),
            enforceAuthorization: false,
            expiresAtUtc: null,
            connectionCancellation.Token);

        var publish = bus.PublishAsync(CreateEvent()).AsTask();

        await publish.WaitAsync(TimeSpan.FromSeconds(1));
        await socket.SendStarted.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(socket.SendCompleted.IsCompleted);

        connectionCancellation.Cancel();
        await connection.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task QueueOverflow_EvictsOnlyStalledClient()
    {
        var bus = new InMemoryScadaEventBus();
        using var workspace = new EngineeringWorkspace();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "false"
        };
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
        using var hub = new TagRealtimeHub(bus, security, runtime: null!);
        using var slowSocket = new ControlledWebSocket(blockSends: true);
        using var healthySocket = new ControlledWebSocket(blockSends: false);
        using var connectionCancellation = new CancellationTokenSource();
        var principal = new SecurityPrincipal("slow-client", null, Array.Empty<string>());
        var slowConnection = hub.HandleAsync(
            slowSocket,
            principal,
            enforceAuthorization: false,
            expiresAtUtc: null,
            connectionCancellation.Token);
        var healthyConnection = hub.HandleAsync(
            healthySocket,
            new SecurityPrincipal("healthy-client", null, Array.Empty<string>()),
            enforceAuthorization: false,
            expiresAtUtc: null,
            connectionCancellation.Token);

        await bus.PublishAsync(CreateEvent());
        await slowSocket.SendStarted.WaitAsync(TimeSpan.FromSeconds(1));
        await healthySocket.WaitForSendAsync(TimeSpan.FromSeconds(1));

        for (var index = 0; index <= TagRealtimeHub.MaximumQueuedMessagesPerClient; index++)
        {
            await bus.PublishAsync(CreateEvent(index));
            await healthySocket.WaitForSendAsync(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(0, hub.RevokeSubject(principal.SubjectId));
        Assert.Equal(1, hub.RevokeSubject("healthy-client"));

        connectionCancellation.Cancel();
        await Task.WhenAll(slowConnection, healthyConnection).WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static TagValueChanged CreateEvent(int value = 1)
    {
        var tag = TagDefinition.Create("Value", "Plant.Value", TagDataType.Int32);
        var current = TagValue.Good(tag.Id, value, "test");
        return new TagValueChanged(tag, null, current, current.Timestamp);
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class ControlledWebSocket(bool blockSends) : WebSocket
    {
        private readonly CancellationTokenSource _disposed = new();
        private readonly SemaphoreSlim _completedSends = new(0);
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;
        private WebSocketState _state = WebSocketState.Open;

        public Task SendStarted => SendStartedSource.Task;
        public Task SendCompleted => SendCompletedSource.Task;
        private TaskCompletionSource SendStartedSource { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource SendCompletedSource { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override WebSocketCloseStatus? CloseStatus => _closeStatus;
        public override string? CloseStatusDescription => _closeStatusDescription;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public async Task WaitForSendAsync(TimeSpan timeout)
        {
            if (!await _completedSends.WaitAsync(timeout))
                throw new TimeoutException("The expected WebSocket send did not complete.");
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
            _disposed.Cancel();
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.Closed;
            _disposed.Cancel();
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_state != WebSocketState.Aborted)
                _state = WebSocketState.Closed;
            _disposed.Cancel();
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            await WaitUntilCanceledAsync(cancellationToken);
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            await WaitUntilCanceledAsync(cancellationToken);
            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) =>
            SendCoreAsync(cancellationToken);

        public override ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) =>
            new(SendCoreAsync(cancellationToken));

        private async Task SendCoreAsync(CancellationToken cancellationToken)
        {
            SendStartedSource.TrySetResult();
            if (blockSends)
            {
                await WaitUntilCanceledAsync(cancellationToken);
                return;
            }

            SendCompletedSource.TrySetResult();
            _completedSends.Release();
        }

        private async Task WaitUntilCanceledAsync(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposed.Token);
            await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
        }
    }
}
