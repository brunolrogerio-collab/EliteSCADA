using System.Net.WebSockets;
using System.Text.Json;

namespace Scada.Drivers.HomeAssistant;

public sealed class HomeAssistantAuthenticationException : IOException
{
    public HomeAssistantAuthenticationException(string message) : base(message) { }
}

public interface IHomeAssistantClient : IAsyncDisposable
{
    bool Connected { get; }
    bool Authenticated { get; }
    string? HomeAssistantVersion { get; }
    ValueTask ConnectAndAuthenticateAsync(ReadOnlyMemory<byte> accessToken, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<HomeAssistantState>> GetStatesAsync(CancellationToken cancellationToken = default);
    ValueTask<int> SubscribeStateChangedAsync(CancellationToken cancellationToken = default);
    ValueTask<HomeAssistantStateChangedEvent> ReceiveStateChangedAsync(CancellationToken cancellationToken = default);
}

public sealed class HomeAssistantWebSocketClient : IHomeAssistantClient
{
    private readonly HomeAssistantConnectionSettings _settings;
    private readonly ClientWebSocket _socket = new();
    private readonly HomeAssistantRequestCorrelator _correlator = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private bool _disposed;

    public HomeAssistantWebSocketClient(HomeAssistantConnectionSettings settings)
    {
        _settings = settings;
        _settings.Validate();
    }

    public bool Connected => _socket.State == WebSocketState.Open;
    public bool Authenticated { get; private set; }
    public string? HomeAssistantVersion { get; private set; }

    public async ValueTask ConnectAndAuthenticateAsync(
        ReadOnlyMemory<byte> accessToken,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (Authenticated) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveConnectTimeout);
        await _socket.ConnectAsync(_settings.WebSocketUri, timeout.Token).ConfigureAwait(false);

        var required = HomeAssistantProtocol.ParseHandshake(await ReceiveFrameAsync(timeout.Token).ConfigureAwait(false));
        if (required.Kind != HomeAssistantHandshakeKind.AuthRequired)
            throw new HomeAssistantAuthenticationException("Home Assistant did not request authentication.");
        HomeAssistantVersion = required.Version;

        await SendAsync(HomeAssistantProtocol.BuildAuth(accessToken.Span), timeout.Token).ConfigureAwait(false);
        var auth = HomeAssistantProtocol.ParseHandshake(await ReceiveFrameAsync(timeout.Token).ConfigureAwait(false));
        HomeAssistantVersion = auth.Version ?? HomeAssistantVersion;
        if (auth.Kind == HomeAssistantHandshakeKind.AuthInvalid)
            throw new HomeAssistantAuthenticationException("Home Assistant authentication failed.");
        if (auth.Kind != HomeAssistantHandshakeKind.AuthOk)
            throw new HomeAssistantAuthenticationException("Home Assistant authentication did not complete.");
        Authenticated = true;
    }

    public async ValueTask<IReadOnlyList<HomeAssistantState>> GetStatesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync("get_states", null, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant get_states failed.");
        return HomeAssistantProtocol.ParseStates(result.Result
            ?? throw new FormatException("Home Assistant get_states returned no result."));
    }

    public async ValueTask<int> SubscribeStateChangedAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync(
            "subscribe_events",
            new Dictionary<string, object?> { ["event_type"] = "state_changed" },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant state_changed subscription failed.");
        return result.Id;
    }

    public async ValueTask<HomeAssistantStateChangedEvent> ReceiveStateChangedAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (true)
        {
            var frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (HomeAssistantProtocol.TryParseStateChangedEvent(frame, out var changed) && changed is not null)
                return changed;

            HomeAssistantCommandResult result;
            try
            {
                result = HomeAssistantProtocol.ParseResult(frame);
            }
            catch (FormatException)
            {
                continue;
            }
            _correlator.TryComplete(result);
        }
    }

    private async ValueTask<HomeAssistantCommandResult> SendCommandAsync(
        string type,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!Authenticated)
            throw new InvalidOperationException("Home Assistant client is not authenticated.");

        var pending = _correlator.Create();
        await SendAsync(HomeAssistantProtocol.BuildCommand(pending.Id, type, arguments), cancellationToken)
            .ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveRequestTimeout);
        try
        {
            while (!pending.Completion.IsCompleted)
            {
                var frame = await ReceiveFrameAsync(timeout.Token).ConfigureAwait(false);
                HomeAssistantCommandResult result;
                try
                {
                    result = HomeAssistantProtocol.ParseResult(frame);
                }
                catch (FormatException)
                {
                    continue;
                }
                _correlator.TryComplete(result);
            }
            return await pending.Completion.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _correlator.TryFail(pending.Id, new TimeoutException(
                $"Home Assistant request timed out after {_settings.EffectiveRequestTimeout}."));
            throw new TimeoutException($"Home Assistant request timed out after {_settings.EffectiveRequestTimeout}.");
        }
        catch
        {
            _correlator.TryCancel(pending.Id, cancellationToken);
            throw;
        }
    }

    private async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async ValueTask<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        await _receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new IOException("Home Assistant WebSocket closed.");
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new IOException("Home Assistant returned a non-text WebSocket message.");
                stream.Write(buffer, 0, result.Count);
                if (stream.Length > HomeAssistantContract.MaximumFrameBytes)
                    throw new IOException("Home Assistant WebSocket message exceeded the maximum allowed size.");
                if (result.EndOfMessage)
                    return stream.ToArray();
            }
        }
        finally
        {
            _receiveGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Authenticated = false;
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }
        _socket.Dispose();
        _sendGate.Dispose();
        _receiveGate.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
