using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Scada.Core.Events;

namespace Scada.Api.Realtime;

public sealed class TagRealtimeHub : IDisposable
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private readonly IDisposable _subscription;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public TagRealtimeHub(Scada.Core.Abstractions.IScadaEventBus eventBus)
    {
        _subscription = eventBus.Subscribe<TagValueChanged>(BroadcastAsync);
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        _clients[clientId] = socket;
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
        foreach (var (id, socket) in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                dead.Add(id);
                continue;
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
            if (_clients.TryRemove(id, out var socket)) socket.Dispose();
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        foreach (var socket in _clients.Values) socket.Dispose();
        _clients.Clear();
    }
}
