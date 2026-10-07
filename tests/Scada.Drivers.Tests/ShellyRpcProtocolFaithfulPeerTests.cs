using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Scada.Drivers.Shelly;

namespace Scada.Drivers.Tests;

public sealed class ShellyRpcProtocolFaithfulPeerTests
{
    [Fact]
    public async Task NativeClient_InteroperatesWithIndependentHttpAndWebSocketPeer()
    {
        await using var peer = await ProtocolPeer.StartAsync();
        var settings = new ShellyConnectionSettings(
            "127.0.0.1",
            peer.Port,
            UseTls: false,
            RequestTimeout: TimeSpan.FromSeconds(3));

        await using var client = new ShellyRpcClient(settings, "elite-l2-peer-test");

        var info = await client.CallHttpAsync(
            "Shelly.GetDeviceInfo",
            null,
            ReadOnlyMemory<byte>.Empty,
            legacyAuthentication: false);
        Assert.Equal("shellyplus1pm-l2", info.GetProperty("id").GetString());

        var status = await client.CallHttpAsync(
            "Shelly.GetStatus",
            null,
            ReadOnlyMemory<byte>.Empty,
            legacyAuthentication: false);
        Assert.False(status.GetProperty("switch:0").GetProperty("output").GetBoolean());

        await client.ConnectNotificationsAsync(
            ReadOnlyMemory<byte>.Empty,
            legacyAuthentication: false);
        Assert.True(client.WebSocketConnected);

        var notification = await client.ReceiveNotificationAsync();
        Assert.Equal("NotifyStatus", notification.Method);
        Assert.Equal(
            31.5,
            notification.Parameters.GetProperty("switch:0").GetProperty("apower").GetDouble());

        Assert.Contains("Shelly.GetDeviceInfo", peer.HttpMethods);
        Assert.Contains("Shelly.GetStatus", peer.HttpMethods);
        Assert.Equal("Shelly.GetStatus", peer.WebSocketBootstrapMethod);
        Assert.Equal("elite-l2-peer-test", peer.WebSocketSource);
    }

    private sealed class ProtocolPeer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        private ProtocolPeer(HttpListener listener, int port)
        {
            _listener = listener;
            Port = port;
            _loop = RunAsync(_cts.Token);
        }

        public int Port { get; }
        public List<string> HttpMethods { get; } = [];
        public string? WebSocketBootstrapMethod { get; private set; }
        public string? WebSocketSource { get; private set; }

        public static Task<ProtocolPeer> StartAsync()
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            return Task.FromResult(new ProtocolPeer(listener, port));
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context, cancellationToken);
                return;
            }

            if (!string.Equals(context.Request.Url?.AbsolutePath, "/rpc", StringComparison.Ordinal) ||
                !string.Equals(context.Request.HttpMethod, "POST", StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            using var requestDocument = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: cancellationToken);
            var root = requestDocument.RootElement;
            var id = root.GetProperty("id").GetInt64();
            var method = root.GetProperty("method").GetString()
                ?? throw new InvalidOperationException("Peer received RPC without method.");
            HttpMethods.Add(method);

            object result = method switch
            {
                "Shelly.GetDeviceInfo" => new
                {
                    id = "shellyplus1pm-l2",
                    mac = "AABBCCDDEEFF",
                    model = "SNSW-001P16EU",
                    gen = 2,
                    ver = "2.0.1",
                    auth_en = false
                },
                "Shelly.GetStatus" => new Dictionary<string, object?>
                {
                    ["switch:0"] = new { id = 0, output = false, apower = 7.0 }
                },
                _ => throw new InvalidOperationException($"Unexpected L2 HTTP RPC method '{method}'.")
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, src = "shellyplus1pm-l2", dst = root.GetProperty("src").GetString(), result });
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
            context.Response.Close();
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var accepted = await context.AcceptWebSocketAsync(null);
            using var socket = accepted.WebSocket;
            var requestBytes = await ReceiveTextAsync(socket, cancellationToken);
            using var requestDocument = JsonDocument.Parse(requestBytes);
            var root = requestDocument.RootElement;
            var id = root.GetProperty("id").GetInt64();
            WebSocketBootstrapMethod = root.GetProperty("method").GetString();
            WebSocketSource = root.GetProperty("src").GetString();

            var response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id,
                src = "shellyplus1pm-l2",
                dst = WebSocketSource,
                result = new Dictionary<string, object?>
                {
                    ["switch:0"] = new { id = 0, output = false, apower = 7.0 }
                }
            });
            await socket.SendAsync(response, WebSocketMessageType.Text, true, cancellationToken);

            var notification = JsonSerializer.SerializeToUtf8Bytes(new
            {
                src = "shellyplus1pm-l2",
                dst = WebSocketSource,
                method = "NotifyStatus",
                @params = new Dictionary<string, object?>
                {
                    ["ts"] = 123.0,
                    ["switch:0"] = new { apower = 31.5 }
                }
            });
            await socket.SendAsync(notification, WebSocketMessageType.Text, true, cancellationToken);

            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var receive = new byte[128];
                    var result = await socket.ReceiveAsync(receive, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "peer-close", CancellationToken.None);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        private static async Task<byte[]> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidOperationException("Expected a text WebSocket RPC request.");
                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage) return stream.ToArray();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            try { await _loop; }
            catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
