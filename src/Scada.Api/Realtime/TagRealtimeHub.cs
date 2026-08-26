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
        CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        _clients[clientId] = new RealtimeClient(socket, principal, enforceAuthorization);
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (WebSocketException) { }
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

        var dead = new List<Guid>();
        foreach (var (id, client) in _clients)
        {
            var socket = client.Socket;
            if (socket.State != WebSocketState.Open)
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
        bool EnforceAuthorization);
}
