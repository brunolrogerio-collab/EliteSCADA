using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Scada.Drivers.Shelly;

public sealed class ShellyRpcException : IOException
{
    public ShellyRpcException(string message, int? rpcCode = null, Exception? inner = null)
        : base(message, inner) => RpcCode = rpcCode;

    public int? RpcCode { get; }
}

public sealed record ShellyNotification(string Method, JsonElement Parameters);

public interface IShellyRpcClient : IAsyncDisposable
{
    bool WebSocketConnected { get; }
    ValueTask<JsonElement> CallHttpAsync(
        string method,
        object? parameters,
        ReadOnlyMemory<byte> password,
        bool legacyAuthentication,
        CancellationToken cancellationToken = default);
    ValueTask ConnectNotificationsAsync(
        ReadOnlyMemory<byte> password,
        bool legacyAuthentication,
        CancellationToken cancellationToken = default);
    ValueTask<ShellyNotification> ReceiveNotificationAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectNotificationsAsync(CancellationToken cancellationToken = default);
}

public sealed class ShellyRpcClient : IShellyRpcClient
{
    private const int MaximumFrameBytes = 1_048_576;
    private readonly ShellyConnectionSettings _settings;
    private readonly HttpClient _http;
    private readonly Func<ClientWebSocket> _webSocketFactory;
    private readonly string _source;
    private readonly SemaphoreSlim _httpGate = new(1, 1);
    private ClientWebSocket? _webSocket;
    private ShellyDigestSession? _webSocketAuth;
    private long _nextId;
    private bool _disposed;

    public ShellyRpcClient(
        ShellyConnectionSettings settings,
        string source,
        HttpClient? httpClient = null,
        Func<ClientWebSocket>? webSocketFactory = null)
    {
        _settings = settings;
        _settings.Validate();
        _source = string.IsNullOrWhiteSpace(source)
            ? throw new ArgumentException("Shelly RPC source is required.", nameof(source))
            : source;
        _http = httpClient ?? new HttpClient();
        _webSocketFactory = webSocketFactory ?? (() => new ClientWebSocket());
    }

    public bool WebSocketConnected => _webSocket?.State == WebSocketState.Open;

    public async ValueTask<JsonElement> CallHttpAsync(
        string method,
        object? parameters,
        ReadOnlyMemory<byte> password,
        bool legacyAuthentication,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _httpGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var id = Interlocked.Increment(ref _nextId);
            var first = await SendHttpAsync(id, method, parameters, auth: null, cancellationToken).ConfigureAwait(false);
            if (first.StatusCode != HttpStatusCode.Unauthorized)
                return ParseSuccessfulHttp(first, id);

            if (password.IsEmpty)
                throw new ShellyRpcException("Shelly authentication is enabled but no protected password is configured.", 401);

            var challenge = ParseHttpChallenge(first, legacyAuthentication);
            var digest = new ShellyDigestSession(_settings.Username, password);
            try
            {
                digest.AcceptChallenge(challenge);
                var auth = digest.BuildAuth(ShellyAuthTransport.Http);
                var retry = await SendHttpAsync(id, method, parameters, auth, cancellationToken).ConfigureAwait(false);
                if (retry.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Stale/current challenges are retried once with the replacement nonce.
                    var refreshed = ParseHttpChallenge(retry, legacyAuthentication);
                    digest.AcceptChallenge(refreshed);
                    retry = await SendHttpAsync(
                        id,
                        method,
                        parameters,
                        digest.BuildAuth(ShellyAuthTransport.Http),
                        cancellationToken).ConfigureAwait(false);
                }
                return ParseSuccessfulHttp(retry, id);
            }
            finally
            {
                digest.Clear();
            }
        }
        finally
        {
            _httpGate.Release();
        }
    }

    public async ValueTask ConnectNotificationsAsync(
        ReadOnlyMemory<byte> password,
        bool legacyAuthentication,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await DisconnectNotificationsAsync(CancellationToken.None).ConfigureAwait(false);

        var socket = _webSocketFactory();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveRequestTimeout);
        await socket.ConnectAsync(_settings.WebSocketRpcUri, timeout.Token).ConfigureAwait(false);
        _webSocket = socket;

        var id = Interlocked.Increment(ref _nextId);
        await SendWebSocketRequestAsync(socket, id, "Shelly.GetStatus", null, null, cancellationToken).ConfigureAwait(false);
        var response = await ReceiveFrameAsync(socket, cancellationToken).ConfigureAwait(false);
        var envelope = ShellyRpcJson.ParseResponse(response);

        if (envelope.Error?.Code == 401)
        {
            if (password.IsEmpty)
                throw new ShellyRpcException("Shelly WebSocket authentication is enabled but no protected password is configured.", 401);

            var challenge = ParseWebSocketChallenge(envelope.Error.Message, legacyAuthentication);
            _webSocketAuth = new ShellyDigestSession(_settings.Username, password);
            _webSocketAuth.AcceptChallenge(challenge);
            await SendWebSocketRequestAsync(
                socket,
                id,
                "Shelly.GetStatus",
                null,
                _webSocketAuth.BuildAuth(ShellyAuthTransport.WebSocket),
                cancellationToken).ConfigureAwait(false);
            response = await ReceiveFrameAsync(socket, cancellationToken).ConfigureAwait(false);
            envelope = ShellyRpcJson.ParseResponse(response);
        }

        if (envelope.Error is not null)
            throw new ShellyRpcException(Sanitize(envelope.Error.Message), envelope.Error.Code);
        if (envelope.Result is null)
            throw new ShellyRpcException("Shelly WebSocket bootstrap returned no result.");

        // The successful request includes a valid src and therefore registers this
        // persistent channel for NotifyStatus/NotifyEvent delivery.
    }

    public async ValueTask<ShellyNotification> ReceiveNotificationAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var socket = _webSocket;
        if (socket is null || socket.State != WebSocketState.Open)
            throw new IOException("Shelly WebSocket is not connected.");

        while (true)
        {
            var frame = await ReceiveFrameAsync(socket, cancellationToken).ConfigureAwait(false);
            if (ShellyRpcJson.TryParseNotification(frame, out var method, out var parameters) && method is not null)
                return new ShellyNotification(method, parameters);

            // Ignore unrelated response frames. Runtime state is updated only by
            // explicit bootstrap/readback or the two accepted notification types.
        }
    }

    public async ValueTask DisconnectNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var socket = Interlocked.Exchange(ref _webSocket, null);
        _webSocketAuth?.Clear();
        _webSocketAuth = null;
        if (socket is null) return;

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "stop",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            socket.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectNotificationsAsync(CancellationToken.None).ConfigureAwait(false);
        _httpGate.Dispose();
        _http.Dispose();
    }

    private async Task<HttpResponseMessage> SendHttpAsync(
        long id,
        string method,
        object? parameters,
        object? auth,
        CancellationToken cancellationToken)
    {
        var payload = ShellyRpcJson.BuildRequest(id, _source, method, parameters, auth);
        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.HttpRpcUri)
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveRequestTimeout);
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Shelly RPC request timed out after {_settings.EffectiveRequestTimeout}.", ex);
        }
    }

    private static JsonElement ParseSuccessfulHttp(HttpResponseMessage response, long expectedId)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new ShellyRpcException($"Shelly HTTP RPC failed with status {(int)response.StatusCode}.");

            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (bytes.Length > MaximumFrameBytes)
                throw new ShellyRpcException("Shelly HTTP RPC response exceeded the maximum frame size.");

            var envelope = ShellyRpcJson.ParseResponse(bytes);
            if (envelope.Id != expectedId)
                throw new ShellyRpcException("Shelly RPC response id did not match the request.");
            if (envelope.Error is not null)
                throw new ShellyRpcException(Sanitize(envelope.Error.Message), envelope.Error.Code);
            return envelope.Result?.Clone()
                ?? throw new ShellyRpcException("Shelly RPC response did not contain a result.");
        }
    }

    private static ShellyDigestChallenge ParseHttpChallenge(HttpResponseMessage response, bool legacyAuthentication)
    {
        using (response)
        {
            var header = response.Headers.WwwAuthenticate.FirstOrDefault();
            if (header is null)
                throw new ShellyRpcException("Shelly HTTP 401 response did not include WWW-Authenticate.", 401);

            var data = ParseDigestHeader(header.ToString(), legacyAuthentication);
            return ShellyDigestChallenge.Parse(data);
        }
    }

    private static JsonElement ParseDigestHeader(string header, bool legacyAuthentication)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var raw = header.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase) ? header[7..] : header;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0) continue;
            var key = token[..separator].Trim();
            var value = token[(separator + 1)..].Trim().Trim('"');
            if (key.Equals("stale", StringComparison.OrdinalIgnoreCase))
                values[key] = bool.TryParse(value, out var stale) && stale;
            else if (key.Equals("nonce", StringComparison.OrdinalIgnoreCase) && legacyAuthentication &&
                     long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var nonce))
                values[key] = nonce;
            else
                values[key] = value;
        }
        return JsonSerializer.SerializeToElement(values);
    }

    private static ShellyDigestChallenge ParseWebSocketChallenge(string message, bool legacyAuthentication)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var challenge = document.RootElement.Clone();
            if (legacyAuthentication && challenge.TryGetProperty("nonce", out var nonce) &&
                nonce.ValueKind == JsonValueKind.String &&
                long.TryParse(nonce.GetString(), out var numeric))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(challenge.GetRawText())
                    ?? new Dictionary<string, object?>();
                map["nonce"] = numeric;
                challenge = JsonSerializer.SerializeToElement(map);
            }
            return ShellyDigestChallenge.Parse(challenge);
        }
        catch (JsonException ex)
        {
            throw new ShellyRpcException("Shelly WebSocket authentication challenge was malformed.", 401, ex);
        }
    }

    private async ValueTask SendWebSocketRequestAsync(
        ClientWebSocket socket,
        long id,
        string method,
        object? parameters,
        object? auth,
        CancellationToken cancellationToken)
    {
        var payload = ShellyRpcJson.BuildRequest(id, _source, method, parameters, auth);
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReceiveFrameAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var result = await socket.ReceiveAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException($"Shelly WebSocket closed: {result.CloseStatus}.");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new IOException("Shelly WebSocket returned a non-text frame.");

            buffer.Write(chunk, 0, result.Count);
            if (buffer.Length > MaximumFrameBytes)
                throw new IOException("Shelly WebSocket frame exceeded the maximum allowed size.");
            if (result.EndOfMessage)
                return buffer.ToArray();
        }
    }

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
