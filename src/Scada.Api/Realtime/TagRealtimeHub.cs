using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Security.Authorization;

namespace Scada.Api.Realtime;

public sealed class TagRealtimeHub : IDisposable
{
    private static readonly TimeSpan MaximumCancelAfter = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private readonly ConcurrentDictionary<Guid, RealtimeClient> _clients = new();
    private readonly IDisposable _subscription;
    private readonly ApiAuthorizationService _security;
    private readonly ScadaRuntimeFacade _runtime;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        _clients[clientId] = new RealtimeClient(socket, principal, enforceAuthorization, expiresAtUtc);
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
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None); }
                catch { }
            }
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
            _ = CloseRevokedClientAsync(removed);
        }

        return revoked;
    }

    private static async Task CloseRevokedClientAsync(RealtimeClient client)
    {
        try
        {
            if (client.Socket.State == WebSocketState.Open)
            {
                await client.Socket.CloseOutputAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "identity revoked",
                    CancellationToken.None);
            }
        }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            try { client.Socket.Dispose(); }
            catch { }
        }
    }

    private async ValueTask BroadcastAsync(TagValueChanged evt)
    {
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
        var dead = new List<Guid>();
        foreach (var (id, client) in _clients)
        {
            var socket = client.Socket;
            if (socket.State != WebSocketState.Open)
            {
                dead.Add(id);
                continue;
            }

            if (client.EnforceAuthorization &&
                client.ExpiresAtUtc.HasValue &&
                client.ExpiresAtUtc.Value <= now)
            {
                dead.Add(id);
                continue;
            }

            if (client.EnforceAuthorization)
            {
                try
                {
                    if (!await _security.CanReadRuntimeTagAsync(
                            client.Principal,
                            _runtime,
                            evt.Tag,
                            CancellationToken.None))
                        continue;
                }
                catch
                {
                    // Realtime read authorization fails closed. A policy/persistence problem must never leak TAG data.
                    continue;
                }
            }

            try
            {
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                dead.Add(id);
            }
            catch (ObjectDisposedException)
            {
                dead.Add(id);
            }
        }

        foreach (var id in dead)
        {
            if (_clients.TryRemove(id, out var client)) client.Socket.Dispose();
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        foreach (var client in _clients.Values) client.Socket.Dispose();
        _clients.Clear();
    }

    private sealed record RealtimeClient(
        WebSocket Socket,
        SecurityPrincipal Principal,
        bool EnforceAuthorization,
        DateTimeOffset? ExpiresAtUtc);
}
