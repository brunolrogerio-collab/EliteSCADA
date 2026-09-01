using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Security.Authorization;

namespace Scada.Api.Realtime;

public sealed class TagRealtimeHub : IDisposable
{
    public const int MaximumQueuedMessagesPerClient = 64;

    private static readonly TimeSpan MaximumCancelAfter = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<Guid, RealtimeClient> _clients = new();
    private readonly IDisposable _subscription;
    private readonly ApiAuthorizationService _security;
    private readonly ScadaRuntimeFacade _runtime;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private int _disposed;

    public TagRealtimeHub(
        Scada.Core.Abstractions.IScadaEventBus eventBus,
        ApiAuthorizationService security,
        ScadaRuntimeFacade runtime)
    {
        _security = security;
        _runtime = runtime;
        _subscription = eventBus.Subscribe<TagValueChanged>(BroadcastAsync);
    }

    public async Task HandleAsync(
        WebSocket socket,
        SecurityPrincipal principal,
        bool enforceAuthorization,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            socket.Dispose();
            return;
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);
        if (enforceAuthorization && expiresAtUtc.HasValue)
        {
            var remaining = expiresAtUtc.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                socket.Dispose();
                return;
            }

            if (remaining <= MaximumCancelAfter)
                lifetime.CancelAfter(remaining);
        }

        var clientId = Guid.NewGuid();
        var client = new RealtimeClient(
            socket,
            principal,
            enforceAuthorization,
            expiresAtUtc,
            lifetime,
            SendTimeout);
        _clients[clientId] = client;
        var sender = client.RunSenderAsync();
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open && !lifetime.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, lifetime.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            _clients.TryRemove(clientId, out _);
            client.Stop(WebSocketCloseStatus.NormalClosure, "closed");
            try { await sender; }
            catch { }
            socket.Dispose();
        }
    }

    /// <summary>
    /// Immediately revokes active realtime sessions for one authenticated subject.
    /// A WebSocket policy-violation close frame is sent before the server disposes the socket so
    /// browser clients can distinguish identity revocation from a transient network disconnect.
    /// </summary>
    public int RevokeSubject(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) return 0;

        var revoked = 0;
        foreach (var (id, client) in _clients)
        {
            if (!string.Equals(client.Principal.SubjectId, subjectId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_clients.TryRemove(id, out var removed)) continue;
            revoked++;
            removed.Stop(WebSocketCloseStatus.PolicyViolation, "identity revoked");
        }

        return revoked;
    }

    private async ValueTask BroadcastAsync(TagValueChanged evt)
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "tagValueChanged",
            tag = new { evt.Tag.Id, evt.Tag.Name, evt.Tag.Path, evt.Tag.EngineeringUnit },
            value = evt.Current.Value,
            quality = evt.Current.Quality.ToString(),
            timestamp = evt.Current.Timestamp,
            source = evt.Current.Source
        }, _jsonOptions);

        var now = DateTimeOffset.UtcNow;
        var deliveries = _clients
            .Select(pair => AuthorizeAndQueueAsync(pair.Key, pair.Value, evt, payload, now))
            .ToArray();
        await Task.WhenAll(deliveries);
    }

    private async Task AuthorizeAndQueueAsync(
        Guid id,
        RealtimeClient client,
        TagValueChanged evt,
        byte[] payload,
        DateTimeOffset now)
    {
        if (client.Socket.State != WebSocketState.Open)
        {
            RemoveClient(id, client, WebSocketCloseStatus.NormalClosure, "closed");
            return;
        }

        if (client.EnforceAuthorization &&
            client.ExpiresAtUtc.HasValue &&
            client.ExpiresAtUtc.Value <= now)
        {
            RemoveClient(id, client, WebSocketCloseStatus.PolicyViolation, "session expired");
            return;
        }

        if (client.EnforceAuthorization)
        {
            try
            {
                if (!await _security.CanReadRuntimeTagAsync(
                        client.Principal,
                        _runtime,
                        evt.Tag,
                        _disposeCancellation.Token))
                    return;
            }
            catch
            {
                // Realtime read authorization fails closed. A policy/persistence problem must never leak TAG data.
                return;
            }
        }

        if (!_clients.TryGetValue(id, out var current) || !ReferenceEquals(current, client)) return;
        if (!client.TryQueue(payload))
            RemoveClient(id, client, WebSocketCloseStatus.PolicyViolation, "realtime client too slow");
    }

    private void RemoveClient(
        Guid id,
        RealtimeClient expected,
        WebSocketCloseStatus closeStatus,
        string description)
    {
        if (_clients.TryRemove(id, out var removed))
        {
            removed.Stop(closeStatus, description);
            return;
        }

        expected.Stop(closeStatus, description);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _subscription.Dispose();
        _disposeCancellation.Cancel();
        foreach (var client in _clients.Values)
            client.Stop(WebSocketCloseStatus.NormalClosure, "server stopping");
        _clients.Clear();
    }

    private sealed class RealtimeClient
    {
        private readonly Channel<ReadOnlyMemory<byte>> _outbound = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(MaximumQueuedMessagesPerClient)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        private readonly CancellationTokenSource _lifetime;
        private readonly TimeSpan _sendTimeout;
        private readonly object _stopGate = new();
        private WebSocketCloseStatus _closeStatus = WebSocketCloseStatus.NormalClosure;
        private string _closeDescription = "closed";
        private bool _stopping;

        public RealtimeClient(
            WebSocket socket,
            SecurityPrincipal principal,
            bool enforceAuthorization,
            DateTimeOffset? expiresAtUtc,
            CancellationTokenSource lifetime,
            TimeSpan sendTimeout)
        {
            Socket = socket;
            Principal = principal;
            EnforceAuthorization = enforceAuthorization;
            ExpiresAtUtc = expiresAtUtc;
            _lifetime = lifetime;
            _sendTimeout = sendTimeout;
        }

        public WebSocket Socket { get; }
        public SecurityPrincipal Principal { get; }
        public bool EnforceAuthorization { get; }
        public DateTimeOffset? ExpiresAtUtc { get; }

        public bool TryQueue(ReadOnlyMemory<byte> payload)
        {
            lock (_stopGate)
            {
                return !_stopping && _outbound.Writer.TryWrite(payload);
            }
        }

        public void Stop(WebSocketCloseStatus closeStatus, string description)
        {
            lock (_stopGate)
            {
                if (_stopping) return;
                _stopping = true;
                _closeStatus = closeStatus;
                _closeDescription = description;
                _outbound.Writer.TryComplete();
                try { _lifetime.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        }

        public async Task RunSenderAsync()
        {
            try
            {
                await foreach (var payload in _outbound.Reader.ReadAllAsync(_lifetime.Token))
                {
                    using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    sendCancellation.CancelAfter(_sendTimeout);
                    try
                    {
                        await Socket.SendAsync(
                            payload,
                            WebSocketMessageType.Text,
                            true,
                            sendCancellation.Token);
                    }
                    catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
                    {
                        Stop(WebSocketCloseStatus.EndpointUnavailable, "realtime send timeout");
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (WebSocketException)
            {
                Stop(WebSocketCloseStatus.EndpointUnavailable, "realtime transport failed");
            }
            catch (ObjectDisposedException) { }
            finally
            {
                _outbound.Writer.TryComplete();
                await CloseOutputAsync();
            }
        }

        private async Task CloseOutputAsync()
        {
            WebSocketCloseStatus closeStatus;
            string closeDescription;
            lock (_stopGate)
            {
                closeStatus = _closeStatus;
                closeDescription = _closeDescription;
            }

            try
            {
                if (Socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
                using var closeCancellation = new CancellationTokenSource(_sendTimeout);
                await Socket.CloseOutputAsync(closeStatus, closeDescription, closeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                Socket.Abort();
            }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
